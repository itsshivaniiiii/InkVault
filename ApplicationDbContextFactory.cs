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
            
            // Try to get connection string from environment variable (for Render)
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
            
            // If not in environment, use appsettings
            if (string.IsNullOrEmpty(connectionString))
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                    .Build();
                
                connectionString = configuration.GetConnectionString("DefaultConnection");
            }
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found and DATABASE_URL not set.");
            }

            // Parse and rebuild connection string explicitly
            try 
            {
                var builder = new NpgsqlConnectionStringBuilder();

                if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
                {
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    
                    builder.Host = uri.Host;
                    builder.Port = uri.Port;
                    builder.Database = uri.AbsolutePath.TrimStart('/');
                    builder.Username = userInfo[0];
                    builder.Password = userInfo.Length > 1 ? userInfo[1] : "";
                }
                else
                {
                    builder.ConnectionString = connectionString;
                }

                // Force required SSL settings
                builder.SslMode = SslMode.Require;
                builder.TrustServerCertificate = true;
                
                connectionString = builder.ToString();
            }
            catch
            {
                // Fallback string replacement if parsing fails
                if (connectionString.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
                {
                     connectionString = connectionString.Replace("sslmode=require", "SSL Mode=Require", StringComparison.OrdinalIgnoreCase);
                }
            }

            optionsBuilder.UseNpgsql(connectionString);
            
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}

