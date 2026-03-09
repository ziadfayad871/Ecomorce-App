using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Member.ViewModels
{
    public class ContactFormVm
    {
        [Required(ErrorMessage = "الاسم مطلوب.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين 2 و 80 حرفًا.")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "عنوان الرسالة مطلوب.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "العنوان يجب أن يكون بين 3 و 120 حرفًا.")]
        public string Subject { get; set; } = "";

        [Required(ErrorMessage = "نص الرسالة مطلوب.")]
        [StringLength(1200, MinimumLength = 10, ErrorMessage = "الرسالة يجب أن تكون بين 10 و 1200 حرف.")]
        public string Message { get; set; } = "";
    }
}
