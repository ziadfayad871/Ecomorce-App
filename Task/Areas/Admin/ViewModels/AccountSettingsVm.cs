using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Admin.ViewModels
{
    public class AccountSettingsVm
    {
        [Required(ErrorMessage = "الاسم مطلوب.")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MinLength(6, ErrorMessage = "كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل.")]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "تأكيد كلمة المرور غير مطابق.")]
        public string? ConfirmNewPassword { get; set; }
    }
}
