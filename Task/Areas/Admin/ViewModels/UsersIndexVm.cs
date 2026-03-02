using DataAccess.Models.Identity;

namespace Task.Areas.Admin.ViewModels
{
    public class UsersIndexVm
    {
        public List<DataAccess.Models.Entities.Member> Members { get; set; } = new();
        public List<ApplicationUser> AdminUsers { get; set; } = new();
    }
}
