using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Member.ViewModels
{
    public class MemberLoginVm
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; } = true;
    }
}