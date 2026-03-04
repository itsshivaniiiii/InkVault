using InkVault.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace InkVault
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            
            // Get environment
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            // Try to get connection string from environment variable (for Render in production)
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
            
            // If not in environment, use appsettings
            if (string.IsNullOrEmpty(connectionString))
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{environment}.json", optional: true)
                    .Build();
                
                connectionString = configuration.GetConnectionString("DefaultConnection");
            }
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found and DATABASE_URL not set.");
            }

            // Convert URI format if needed
            if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
            {
                var uri = new Uri(connectionString);
                var userInfo = uri.UserInfo.Split(':');
                connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={(userInfo.Length > 1 ? userInfo[1] : "")};";
            }

            // Parse through builder so SSL flags are always applied cleanly
            var csb = new NpgsqlConnectionStringBuilder(connectionString);

            if (!environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                // Production / Aiven: require SSL and trust the server certificate
                csb.SslMode = SslMode.Require;
                csb.TrustServerCertificate = true;
                Console.WriteLine("[DbContextFactory] SSL: SslMode=Require, TrustServerCertificate=true");
            }

            optionsBuilder.UseNpgsql(csb.ToString());
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}


