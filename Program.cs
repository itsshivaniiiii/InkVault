using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using InkVault.Data;
using InkVault.Models;
using InkVault.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - Use connection string from config or environment variable (for production)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // Fall back to DATABASE_URL environment variable (used by Render)
    connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string not configured");
}

try 
{
    var builder2 = new Npgsql.NpgsqlConnectionStringBuilder();

    if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
    {
        // Parse Render's URI format
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        
        builder2.Host = uri.Host;
        builder2.Port = uri.Port;
        builder2.Database = uri.AbsolutePath.TrimStart('/');
        builder2.Username = userInfo[0];
        builder2.Password = userInfo.Length > 1 ? userInfo[1] : "";
    }
    else
    {
        // Parse standard connection string
        builder2.ConnectionString = connectionString;
    }

    // Force required SSL settings
    builder2.SslMode = Npgsql.SslMode.Require;
    builder2.TrustServerCertificate = true; // For Aiven/Render reliability
    
    // Update connection string
    connectionString = builder2.ToString();
    
    Console.WriteLine($"Using connection (masked): Host={builder2.Host}; Database={builder2.Database}; Username={builder2.Username}; SslMode={builder2.SslMode}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error parsing connection string: {ex.Message}");
    // Fallback: simple string replacement to fix common SslMode issues if parsing fails
    if (connectionString.Contains("sslmode=Required", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = connectionString.Replace("sslmode=Required", "SSL Mode=Require", StringComparison.OrdinalIgnoreCase);
        connectionString = connectionString.Replace("sslmode=required", "SSL Mode=Require", StringComparison.OrdinalIgnoreCase);
    }
    // Also fix lowercase 'require' just in case
    if (connectionString.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase)) 
    {
        connectionString = connectionString.Replace("sslmode=require", "SSL Mode=Require", StringComparison.OrdinalIgnoreCase);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Data Protection for antiforgery tokens to persist across restarts
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// Identity with persistent login support
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
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
    
    // In development, allow cookies over HTTP for testing
    // In production, require HTTPS (secure)
    if (builder.Environment.IsProduction())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
    else
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;  // Allow HTTP in development
    }
    
    options.ExpireTimeSpan = TimeSpan.FromDays(14); // Remember me duration: 14 days
    options.SlidingExpiration = true; // Extend expiration on each request
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    
    // In development, reduce cookie timeout for testing
    if (!builder.Environment.IsProduction())
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // 7 days in development
    }
});

// Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOTPService, OTPService>();
builder.Services.AddScoped<IBirthdayService, BirthdayService>();

// Hosted service for daily birthday email check
// Temporarily disabled for debugging - enable after fixing login
// builder.Services.AddHostedService<BirthdayBackgroundService>();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
