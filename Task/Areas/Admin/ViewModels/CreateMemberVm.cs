using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Admin.ViewModels
{
    public class CreateMemberVm
    {
        [Required, MaxLength(120)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password), MinLength(6)]
        public string Password { get; set; } = "";
    }
}
