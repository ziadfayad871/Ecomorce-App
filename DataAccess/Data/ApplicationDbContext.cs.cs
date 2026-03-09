using DataAccess.Models;
using Core.Domain.Entities;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        //  Members table (manual hash)
        public DbSet<Member> Members => Set<Member>();

        // Store tables
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Offer> Offers => Set<Offer>();
        public DbSet<MemberFavorite> MemberFavorites => Set<MemberFavorite>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Member>()
                .HasIndex(m => m.Email)
                .IsUnique();

            builder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId);

            builder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            builder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            builder.Entity<Order>()
                .HasIndex(o => o.PaymentSessionId)
                .IsUnique(false);

            builder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(18, 2);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Offer>()
                .HasIndex(o => o.Code)
                .IsUnique();

            builder.Entity<Offer>()
                .Property(o => o.DiscountPercent)
                .HasPrecision(5, 2);

            builder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId);

            builder.Entity<MemberFavorite>()
                .HasIndex(x => new { x.MemberId, x.ProductId })
                .IsUnique();

            builder.Entity<MemberFavorite>()
                .HasOne(x => x.Member)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MemberFavorite>()
                .HasOne(x => x.Product)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
