namespace Task.Areas.Admin.ViewModels
{
    public class RoleSummaryVm
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int UsersCount { get; set; }
        public string? BrowseAction { get; set; }
    }
}
