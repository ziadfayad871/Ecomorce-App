using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Member.ViewModels;

public class MemberResetPasswordVm
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز التحقق مطلوب.")]
    [StringLength(8, MinimumLength = 4, ErrorMessage = "رمز التحقق غير صحيح.")]
    public string OtpCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة.")]
    [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب.")]
    [Compare(nameof(NewPassword), ErrorMessage = "تأكيد كلمة المرور غير مطابق.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
