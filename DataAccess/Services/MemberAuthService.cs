using System.Net;
using System.Security.Cryptography;
using Core.Application.Common.Communication;
using Core.Application.Common.Identity;
using DataAccess.Data;
using Core.Domain.Entities;
using DataAccess.Options;
using DataAccess.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAccess.Services
{
    public class MemberAuthService : IMemberAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<MemberAuthService> _logger;
        private readonly PasswordResetOtpOptions _otpOptions;

        public MemberAuthService(
            ApplicationDbContext db,
            IEmailSender emailSender,
            IOptions<PasswordResetOtpOptions> otpOptions,
            ILogger<MemberAuthService> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _otpOptions = otpOptions.Value;
            _logger = logger;
        }

        public async Task<int> RegisterAsync(string fullName, string email, string password)
        {
            email = NormalizeEmail(email);
            var exists = await _db.Members.AnyAsync(m => m.Email == email);
            if (exists) throw new Exception("Email already exists");

            var member = new Member
            {
                FullName = fullName,
                Email = email,
                PasswordHash = ManualHasher.Hash(password),
                IsActive = true
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            return member.Id;
        }

        public async Task<Member?> LoginAsync(string email, string password)
        {
            email = NormalizeEmail(email);
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email && m.IsActive);
            if (member == null) return null;

            return ManualHasher.Verify(password, member.PasswordHash) ? member : null;
        }

        public async Task<Member?> FindByEmailAsync(string email, bool activeOnly = false)
        {
            email = NormalizeEmail(email);
            var query = _db.Members.AsQueryable();

            if (activeOnly)
            {
                query = query.Where(m => m.IsActive);
            }

            return await query.FirstOrDefaultAsync(m => m.Email == email);
        }

        public async Task<Member> RegisterExternalAsync(string fullName, string email)
        {
            email = NormalizeEmail(email);
            var existing = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);
            if (existing != null)
            {
                return existing;
            }

            var displayName = string.IsNullOrWhiteSpace(fullName)
                ? email.Split('@')[0]
                : fullName.Trim();

            if (displayName.Length > 120)
            {
                displayName = displayName[..120];
            }

            var member = new Member
            {
                FullName = displayName,
                Email = email,
                PasswordHash = ManualHasher.Hash($"ext-{Guid.NewGuid():N}"),
                IsActive = true
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            return member;
        }

        public async Task<RequestPasswordResetOtpResult> SendPasswordResetOtpAsync(string email, CancellationToken cancellationToken = default)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return new RequestPasswordResetOtpResult(false, "البريد الإلكتروني مطلوب.");
            }

            var member = await _db.Members
                .Include(m => m.PasswordResetOtps)
                .FirstOrDefaultAsync(m => m.Email == email && m.IsActive, cancellationToken);

            if (member == null)
            {
                return new RequestPasswordResetOtpResult(true, "إذا كان البريد الإلكتروني مسجلاً، فسيصل رمز التحقق خلال لحظات.");
            }

            var now = DateTime.UtcNow;
            foreach (var existingOtp in member.PasswordResetOtps.Where(x => x.UsedAtUtc == null && x.ExpiresAtUtc > now))
            {
                existingOtp.UsedAtUtc = now;
            }

            var code = GenerateOtpCode();
            var otp = new MemberPasswordResetOtp
            {
                MemberId = member.Id,
                CodeHash = ManualHasher.Hash(code),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(GetExpiryMinutes())
            };

            _db.MemberPasswordResetOtps.Add(otp);
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                await _emailSender.SendAsync(
                    member.Email,
                    "رمز استعادة كلمة المرور",
                    BuildPasswordResetEmail(member.FullName, code, GetExpiryMinutes()),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset OTP for member {MemberId}", member.Id);
                _db.MemberPasswordResetOtps.Remove(otp);
                await _db.SaveChangesAsync(cancellationToken);
                return new RequestPasswordResetOtpResult(false, "تعذر إرسال رمز التحقق حالياً. حاولي مرة أخرى بعد قليل.");
            }

            return new RequestPasswordResetOtpResult(true, "إذا كان البريد الإلكتروني مسجلاً، فسيصل رمز التحقق خلال لحظات.");
        }

        public async Task<ResetPasswordWithOtpResult> ResetPasswordWithOtpAsync(string email, string otpCode, string newPassword, CancellationToken cancellationToken = default)
        {
            email = NormalizeEmail(email);
            otpCode = (otpCode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(newPassword))
            {
                return new ResetPasswordWithOtpResult(false, "البيانات المطلوبة غير مكتملة.");
            }

            var now = DateTime.UtcNow;
            var member = await _db.Members
                .Include(m => m.PasswordResetOtps)
                .FirstOrDefaultAsync(m => m.Email == email && m.IsActive, cancellationToken);

            if (member == null)
            {
                return new ResetPasswordWithOtpResult(false, "رمز التحقق غير صحيح أو انتهت صلاحيته.");
            }

            var activeOtp = member.PasswordResetOtps
                .Where(x => x.UsedAtUtc == null && x.ExpiresAtUtc > now)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault();

            if (activeOtp == null)
            {
                return new ResetPasswordWithOtpResult(false, "رمز التحقق غير صحيح أو انتهت صلاحيته.");
            }

            var maxFailedAttempts = GetMaxFailedAttempts();
            if (activeOtp.FailedAttempts >= maxFailedAttempts)
            {
                activeOtp.UsedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                return new ResetPasswordWithOtpResult(false, "تم تجاوز عدد المحاولات المسموح. اطلب رمزاً جديداً.");
            }

            if (!ManualHasher.Verify(otpCode, activeOtp.CodeHash))
            {
                activeOtp.FailedAttempts++;
                activeOtp.LastAttemptAtUtc = now;
                if (activeOtp.FailedAttempts >= maxFailedAttempts)
                {
                    activeOtp.UsedAtUtc = now;
                }

                await _db.SaveChangesAsync(cancellationToken);
                return new ResetPasswordWithOtpResult(false, activeOtp.UsedAtUtc == null
                    ? "رمز التحقق غير صحيح."
                    : "تم تجاوز عدد المحاولات المسموح. اطلب رمزاً جديداً.");
            }

            member.PasswordHash = ManualHasher.Hash(newPassword);
            activeOtp.UsedAtUtc = now;
            activeOtp.LastAttemptAtUtc = now;

            foreach (var otherOtp in member.PasswordResetOtps.Where(x => x.Id != activeOtp.Id && x.UsedAtUtc == null))
            {
                otherOtp.UsedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return new ResetPasswordWithOtpResult(true, "تم تحديث كلمة المرور بنجاح.");
        }

        private static string NormalizeEmail(string email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        private int GetOtpLength() => Math.Clamp(_otpOptions.CodeLength, 4, 8);

        private int GetExpiryMinutes() => Math.Clamp(_otpOptions.ExpiryMinutes, 5, 30);

        private int GetMaxFailedAttempts() => Math.Clamp(_otpOptions.MaxFailedAttempts, 3, 10);

        private string GenerateOtpCode()
        {
            var length = GetOtpLength();
            var chars = new char[length];

            for (var i = 0; i < length; i++)
            {
                chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
            }

            return new string(chars);
        }

        private string BuildPasswordResetEmail(string fullName, string code, int expiryMinutes)
        {
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "عميلنا" : fullName.Trim());

            return $$"""
                     <!DOCTYPE html>
                     <html lang="ar" dir="rtl">
                     <body style="margin:0;padding:24px;background:#f8fafc;font-family:'Segoe UI',Tahoma,sans-serif;color:#0f172a;">
                         <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:20px;padding:32px;">
                             <div style="text-align:center;margin-bottom:24px;">
                                 <div style="display:inline-block;padding:12px 18px;border-radius:999px;background:#e0f2fe;color:#0369a1;font-weight:700;">أناقة بلس</div>
                             </div>
                             <h2 style="margin:0 0 12px;font-size:24px;color:#0c4a6e;">استعادة كلمة المرور</h2>
                             <p style="margin:0 0 16px;line-height:1.9;">مرحباً {{safeName}}،</p>
                             <p style="margin:0 0 20px;line-height:1.9;">استخدم رمز التحقق التالي لإكمال تغيير كلمة المرور. صلاحية الرمز {{expiryMinutes}} دقائق.</p>
                             <div style="margin:0 0 24px;padding:18px;border-radius:16px;background:#f0f9ff;border:1px dashed #7dd3fc;text-align:center;">
                                 <div style="font-size:34px;font-weight:800;letter-spacing:8px;color:#0369a1;">{{code}}</div>
                             </div>
                             <p style="margin:0;line-height:1.9;color:#475569;">إذا لم تطلب هذا الرمز يمكنك تجاهل الرسالة.</p>
                         </div>
                     </body>
                     </html>
                     """;
        }
    }
}
