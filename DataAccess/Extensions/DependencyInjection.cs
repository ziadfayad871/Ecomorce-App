using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
using Core.Application.Common.Communication;
using Core.Application.Common.Files;
using Core.Application.Common.Identity;
using Core.Application.Common.Persistence;
using Core.Application.Members.Contracts;
using DataAccess.Repositories;
using DataAccess.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccessServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IAdminActivityLogRepository, AdminActivityLogRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        
        services.AddScoped<IMemberAuthService, MemberAuthService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IAdminActivityService, AdminActivityService>();
        services.AddScoped<IMemberPanelService, MemberPanelService>();
        services.AddScoped<IDbInitializer, DataAccess.Data.Initializer.DbInitializer>();

        return services;
    }
}
