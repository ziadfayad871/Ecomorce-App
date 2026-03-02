using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Member.ViewModels
{
    public class MemberRegisterVm
    {
        [Required, MaxLength(120)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, MinLength(6)]
        public string Password { get; set; } = "";
    }
}