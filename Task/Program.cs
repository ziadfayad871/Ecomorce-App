using DataAccess.Data;

using DataAccess.Extensions;
using DataAccess.Options;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<PasswordResetOtpOptions>(builder.Configuration.GetSection("PasswordResetOtp"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Admin/Account/Login";
    opt.AccessDeniedPath = "/Admin/Account/Login";
});


builder.Services.AddAuthentication()
    .AddCookie("MemberCookie", opt =>
    {
        opt.LoginPath = "/Member/Account/Login";
        opt.AccessDeniedPath = "/Member/Account/Login";
    })
    .AddGoogle("Google", opt =>
    {
        opt.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        opt.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    })
    .AddMicrosoftAccount("Microsoft", opt =>
    {
        opt.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? "";
        opt.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "";
    });

builder.Services.AddDataAccessServices();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", context =>
{
    context.Response.Redirect("/Member/Home/Index");
    return System.Threading.Tasks.Task.CompletedTask;
});

app.MapControllerRoute(
    name: "Areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize DB
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<Core.Application.Common.Persistence.IDbInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

