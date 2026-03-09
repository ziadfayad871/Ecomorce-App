using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
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
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IMemberAuthService, MemberAuthService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IAdminActivityService, AdminActivityService>();
        services.AddScoped<IMemberPanelService, MemberPanelService>();

        return services;
    }
}
