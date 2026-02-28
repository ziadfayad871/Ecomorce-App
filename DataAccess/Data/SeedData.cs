using DataAccess.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Data
{
    public class SeedData
    {

        public static async System.Threading.Tasks.Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Admin>>();

            // Ensure roles
            string[] roles = new[] { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Ensure admin user
            var adminEmail = "admin@local.com"; // Change this to admin email [ dont forget ]
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var admin = new Admin
                {
                    UserName = "admin", // change this to admin username [ dont forget ]
                    Email = adminEmail,
                    Name = "Administrator", // change this to admin name [ dont forget ]
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@123"); // Change this to a strong password [ dont forget ]
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin"); 
                }
            }
        }
    }
}
