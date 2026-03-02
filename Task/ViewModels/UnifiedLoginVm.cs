using System.ComponentModel.DataAnnotations;

namespace Task.ViewModels
{
    public class UnifiedLoginVm
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }
}
