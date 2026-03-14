using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Member.ViewModels;

public class MemberForgotPasswordVm
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string Email { get; set; } = string.Empty;
}
