using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using InkVault.Data;
using InkVault.Models;
using InkVault.Services;
using InkVault.Filters;

var builder = WebApplication.CreateBuilder(args);

// Get connection string - try DATABASE_URL (Render/Aiven) first, then config
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(connectionString))
{
    // Convert postgres:// URI format to Npgsql format if needed
    if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={(userInfo.Length > 1 ? userInfo[1] : "")};";
    }
    Console.WriteLine("[STARTUP] Using DATABASE_URL from environment");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine("[STARTUP] Using connection string from configuration");
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("No database connection string found. Set DATABASE_URL or ConnectionStrings__DefaultConnection.");
}

// Ensure SSL settings for production (Aiven requires SSL + trust server cert)
if (builder.Environment.IsProduction())
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
    {
        SslMode = Npgsql.SslMode.Require,
        TrustServerCertificate = true
    };
    connectionString = csb.ToString();
}

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Data Protection
if (builder.Environment.IsProduction())
{
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<ApplicationDbContext>();
}

// Identity with persistent login support and lockout configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    
    // Lockout configuration
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(1);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure persistent authentication cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    
    if (builder.Environment.IsProduction())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
    else
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    }
    
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOTPService, OTPService>();
builder.Services.AddScoped<IBirthdayService, BirthdayService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHttpClient<IAIEnhancementService, AIEnhancementService>();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// MVC with First Login Filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<FirstLoginFilter>();
});

// Register First Login Filter
builder.Services.AddScoped<FirstLoginFilter>();

// Configure forwarded headers for reverse proxy (Render/Docker)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Auto-apply pending migrations on startup (essential for Render deployment)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        Console.WriteLine("[STARTUP] Applying pending database migrations...");
        db.Database.Migrate();
        Console.WriteLine("[STARTUP] Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[STARTUP] Migration error: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Only redirect to HTTPS in non-container environments
// Render/Docker handles HTTPS at the reverse proxy level
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
{
    app.UseForwardedHeaders();
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
