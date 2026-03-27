using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using InkVault.Data;
using InkVault.Models;
using InkVault.Services;
using InkVault.Filters;

var builder = WebApplication.CreateBuilder(args);

// Allow DateTime with Kind=Unspecified to be written as UTC to PostgreSQL
// (HTML date inputs produce Unspecified kind; Npgsql 6+ rejects them by default)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Get connection string - try DATABASE_URL (Render/Aiven) first, then config
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(connectionString))
{
    // It might be a URI (e.g., postgres://user:pass@host:port/db) or a raw Npgsql string.
    // Try to parse as a URI. A valid DB URI will have a scheme and user info.
    if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
    {
        var userInfo = uri.UserInfo.Split(':');
        var host = uri.Host;
        var port = uri.Port;

        // Handle cases where the host part of the URI includes a protocol, e.g., from Aiven/Render.
        // The Npgsql driver expects a clean hostname for DNS resolution.
        if (host.Contains("://"))
        {
            // Example: tcp://pg-d65e37a-cmr-c7af.h.aivencloud.com
            host = host.Substring(host.IndexOf("://") + 3);
        }

        connectionString = $"Host={host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={(userInfo.Length > 1 ? userInfo[1] : "")};";
        Console.WriteLine("[STARTUP] Using DATABASE_URL (parsed as URI) from environment");
    }
    else
    {
        // Assume it's already a valid Npgsql connection string
        Console.WriteLine("[STARTUP] Using DATABASE_URL (raw) from environment");
    }
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
    options.UseNpgsql(connectionString)
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

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
builder.Services.AddHttpClient<IEmailService, EmailService>();
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

// Auto-apply pending migrations on startup with retry (Aiven/Render cold start can be slow)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var maxRetries = 5;
    var delay = 5;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Console.WriteLine($"[STARTUP] Applying migrations (attempt {attempt}/{maxRetries})...");
            db.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
            db.Database.Migrate();
            Console.WriteLine("[STARTUP] Database migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STARTUP] Migration attempt {attempt} failed: {ex.Message}");
            if (attempt == maxRetries)
            {
                // On final failure, crash loudly so Render shows the real error
                Console.WriteLine("[STARTUP] FATAL: All migration attempts failed. Halting startup.");
                throw;
            }
            Console.WriteLine($"[STARTUP] Retrying in {delay}s...");
            Thread.Sleep(TimeSpan.FromSeconds(delay));
            delay *= 2; // exponential backoff: 5s, 10s, 20s, 40s
        }
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

// Lightweight health endpoint for Render (no auth, no DB)
app.MapGet("/health", () => Results.Ok("healthy"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
