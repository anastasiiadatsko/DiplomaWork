using HabitFlow.BLL.Interfaces;
using HabitFlow.BLL.Services;
using HabitFlow.DAL.Context;
using HabitFlow.DAL.Repositories;
using HabitFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/habitflow.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IHabitRepository, HabitRepository>();
builder.Services.AddScoped<IHabitLogRepository, HabitLogRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddHttpClient<HabitFlow.BLL.Services.CoachService>();

builder.Services.AddScoped<HabitFlow.BLL.Interfaces.ICoachService,
                            HabitFlow.BLL.Services.CoachService>();

builder.Services.AddScoped<ISharedHabitService, SharedHabitService>();

builder.Services.AddScoped<ISharedHabitRepository, SharedHabitRepository>();

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();