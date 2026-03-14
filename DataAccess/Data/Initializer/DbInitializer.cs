using Core.Application.Common.Persistence;
using Core.Domain.Entities;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataAccess.Data.Initializer;

public class DbInitializer : IDbInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(IServiceProvider serviceProvider, ILogger<DbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();

            if (db.Database.GetPendingMigrations().Any())
            {
                await db.Database.MigrateAsync();
            }

            await SeedIdentityAsync(_serviceProvider);
            await SeedStoreAsync(db);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    private static async System.Threading.Tasks.Task SeedIdentityAsync(IServiceProvider sp)
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

    private static async System.Threading.Tasks.Task SeedStoreAsync(ApplicationDbContext db)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Men" },
                new Category { Name = "Women" },
                new Category { Name = "Kids" },
                new Category { Name = "Basics" }
            );

            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            var menId = await db.Categories.Where(c => c.Name == "Men").Select(c => c.Id).FirstOrDefaultAsync();
            var womenId = await db.Categories.Where(c => c.Name == "Women").Select(c => c.Id).FirstOrDefaultAsync();
            var kidsId = await db.Categories.Where(c => c.Name == "Kids").Select(c => c.Id).FirstOrDefaultAsync();
            var basicsId = await db.Categories.Where(c => c.Name == "Basics").Select(c => c.Id).FirstOrDefaultAsync();

            var fallbackCategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync();
            if (fallbackCategoryId != 0)
            {
                db.Products.AddRange(
                    new Product { Name = "Denim Jacket", Price = 1199m, StockQuantity = 25, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                    new Product { Name = "Casual Dress", Price = 1349m, StockQuantity = 18, CategoryId = womenId != 0 ? womenId : fallbackCategoryId },
                    new Product { Name = "Cotton T-Shirt", Price = 399m, StockQuantity = 55, CategoryId = basicsId != 0 ? basicsId : fallbackCategoryId },
                    new Product { Name = "Winter Hoodie", Price = 899m, StockQuantity = 10, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                    new Product { Name = "Gabardine Pants", Price = 749m, StockQuantity = 7, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                    new Product { Name = "Kids Pajamas", Price = 549m, StockQuantity = 5, CategoryId = kidsId != 0 ? kidsId : fallbackCategoryId }
                );

                await db.SaveChangesAsync();
            }
        }

        if (!await db.Offers.AnyAsync())
        {
            db.Offers.AddRange(
                new Offer
                {
                    Title = "خصم ترحيبي",
                    Code = "WELCOME10",
                    DiscountPercent = 10,
                    IsActive = true,
                    StartsAtUtc = DateTime.UtcNow.AddDays(-7),
                    EndsAtUtc = DateTime.UtcNow.AddMonths(2)
                },
                new Offer
                {
                    Title = "عروض نهاية الموسم",
                    Code = "SEASON15",
                    DiscountPercent = 15,
                    IsActive = true,
                    StartsAtUtc = DateTime.UtcNow.AddDays(-2),
                    EndsAtUtc = DateTime.UtcNow.AddDays(20)
                }
            );

            await db.SaveChangesAsync();
        }
    }
}
