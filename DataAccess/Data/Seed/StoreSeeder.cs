using DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Data.Seed
{
    public static class StoreSeeder
    {
        public static async ValueTask SeedAsync(IServiceProvider sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();

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

            if (await db.Products.AnyAsync())
            {
                return;
            }

            var menId = await db.Categories.Where(c => c.Name == "Men").Select(c => c.Id).FirstOrDefaultAsync();
            var womenId = await db.Categories.Where(c => c.Name == "Women").Select(c => c.Id).FirstOrDefaultAsync();
            var kidsId = await db.Categories.Where(c => c.Name == "Kids").Select(c => c.Id).FirstOrDefaultAsync();
            var basicsId = await db.Categories.Where(c => c.Name == "Basics").Select(c => c.Id).FirstOrDefaultAsync();

            // Use first category as fallback to avoid invalid FK if names are customized by user.
            var fallbackCategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync();
            if (fallbackCategoryId == 0)
            {
                return;
            }

            db.Products.AddRange(
                new Product { Name = "Denim Jacket", Price = 1199m, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                new Product { Name = "Casual Dress", Price = 1349m, CategoryId = womenId != 0 ? womenId : fallbackCategoryId },
                new Product { Name = "Cotton T-Shirt", Price = 399m, CategoryId = basicsId != 0 ? basicsId : fallbackCategoryId },
                new Product { Name = "Winter Hoodie", Price = 899m, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                new Product { Name = "Gabardine Pants", Price = 749m, CategoryId = menId != 0 ? menId : fallbackCategoryId },
                new Product { Name = "Kids Pajamas", Price = 549m, CategoryId = kidsId != 0 ? kidsId : fallbackCategoryId }
            );

            await db.SaveChangesAsync();
        }
    }
}
