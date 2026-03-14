using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Admin.ViewModels
{
    public class MemberEditVm
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = "";

        public bool IsActive { get; set; }

        public string? SelectedRole { get; set; }

        public List<string> AvailableRoles { get; set; } = new();
    }
}
