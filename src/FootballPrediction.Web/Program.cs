using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.ML.Prediction;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// ── Identity + SQLite ──
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Configuration
builder.Services.Configure<ModelSettings>(
    builder.Configuration.GetSection(ModelSettings.SectionName));
builder.Services.Configure<OddsApiOptions>(
    builder.Configuration.GetSection(OddsApiOptions.SectionName));
builder.Services.Configure<AdminSeedOptions>(
    builder.Configuration.GetSection(AdminSeedOptions.SectionName));

// Services
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<ModelSettings>>().Value;
    var predictor = new MatchPredictor();
    var modelPath = Path.Combine(Directory.GetCurrentDirectory(), settings.BinaryModelPath);
    if (File.Exists(modelPath))
        predictor.LoadModel(modelPath);
    return predictor;
});
builder.Services.AddTransient<ICsvParser, CsvParserService>();
builder.Services.AddSingleton<IPredictionService, PredictionService>();

// ── Odds API ──
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IOddsFetcherService, OddsFetcherService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// ── Seed admin user ──
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedOpts = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
    db.Database.EnsureCreated();
    await SeedAdminAsync(userManager, roleManager, seedOpts);
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

static async Task SeedAdminAsync(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AdminSeedOptions opts)
{
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    var adminUser = await userManager.FindByEmailAsync(opts.Email);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = opts.Email,
            Email = opts.Email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, opts.Password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}
