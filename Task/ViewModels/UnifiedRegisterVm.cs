using System.ComponentModel.DataAnnotations;

namespace Task.ViewModels
{
    public class UnifiedRegisterVm
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
        [MaxLength(120, ErrorMessage = "الاسم لا يجب أن يتجاوز 120 حرف.")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.")]
        public string Password { get; set; } = "";

        public string? ReturnUrl { get; set; }
    }
}
