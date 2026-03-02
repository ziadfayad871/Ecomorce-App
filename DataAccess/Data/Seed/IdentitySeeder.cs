using Microsoft.AspNetCore.Identity;
using DataAccess.Models.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Data.Seed
{
    public static class IdentitySeeder
    {
        public static async ValueTask SeedAsync(IServiceProvider sp)
        {
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleMgr.RoleExistsAsync("Admin"))
                await roleMgr.CreateAsync(new IdentityRole("Admin"));

            var adminEmail = "admin@shop.com";
            var admin = await userMgr.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Admin"
                };

                var createRes = await userMgr.CreateAsync(admin, "Admin@12345");
                if (!createRes.Succeeded)
                {
                    var errs = string.Join(" | ", createRes.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Admin seed create failed: {errs}");
                }
            }

            if (!await userMgr.IsInRoleAsync(admin, "Admin"))
            {
                var roleRes = await userMgr.AddToRoleAsync(admin, "Admin");
                if (!roleRes.Succeeded)
                {
                    var errs = string.Join(" | ", roleRes.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Admin seed role assignment failed: {errs}");
                }
            }
        }
    }
}
