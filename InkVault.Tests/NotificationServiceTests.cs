using System;
using System.Linq;
using System.Threading.Tasks;
using InkVault.Data;
using InkVault.Models;
using InkVault.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InkVault.Tests
{
    public class NotificationServiceTests
    {
        private Mock<IEmailService> _mockEmailService;
        private Mock<ILogger<NotificationService>> _mockLogger;

        public NotificationServiceTests()
        {
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<NotificationService>>();
        }

        private ApplicationDbContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetOrCreateNotificationPreferencesAsync_ShouldCreate_WhenNotFound()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var context = GetInMemoryContext(dbName);
            var service = new NotificationService(context, _mockEmailService.Object, _mockLogger.Object);
            var userId = "test-user-1";

            // Act
            var result = await service.GetOrCreateNotificationPreferencesAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.True(context.NotificationPreferences.Any(n => n.UserId == userId));
        }

        [Fact]
        public async Task GetOrCreateNotificationPreferencesAsync_ShouldReturnExisting_WhenFound()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var context = GetInMemoryContext(dbName);
            var userId = "test-user-2";
            var existing = new NotificationPreference { UserId = userId };
            context.NotificationPreferences.Add(existing);
            await context.SaveChangesAsync();

            var service = new NotificationService(context, _mockEmailService.Object, _mockLogger.Object);

            // Act
            var result = await service.GetOrCreateNotificationPreferencesAsync(userId);

            // Assert
            Assert.Same(existing, result);
        }
    }
}
