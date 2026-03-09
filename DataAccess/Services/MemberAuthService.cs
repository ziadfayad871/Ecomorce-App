using Core.Application.Common.Identity;
using DataAccess.Data;
using Core.Domain.Entities;
using DataAccess.Security;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Services
{
    public class MemberAuthService : IMemberAuthService
    {
        private readonly ApplicationDbContext _db;

        public MemberAuthService(ApplicationDbContext db) => _db = db;

        public async Task<int> RegisterAsync(string fullName, string email, string password)
        {
            email = email.Trim().ToLower();
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
            email = email.Trim().ToLower();
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email && m.IsActive);
            if (member == null) return null;

            return ManualHasher.Verify(password, member.PasswordHash) ? member : null;
        }

        public async Task<Member?> FindByEmailAsync(string email, bool activeOnly = false)
        {
            email = email.Trim().ToLower();
            var query = _db.Members.AsQueryable();

            if (activeOnly)
            {
                query = query.Where(m => m.IsActive);
            }

            return await query.FirstOrDefaultAsync(m => m.Email == email);
        }

        public async Task<Member> RegisterExternalAsync(string fullName, string email)
        {
            email = email.Trim().ToLower();
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
    }
}
