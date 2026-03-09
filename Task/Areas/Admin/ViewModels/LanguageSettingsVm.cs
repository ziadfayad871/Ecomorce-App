using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Admin.ViewModels
{
    public class LanguageSettingsVm
    {
        [Required]
        public string SelectedLanguage { get; set; } = "ar";
    }
}
