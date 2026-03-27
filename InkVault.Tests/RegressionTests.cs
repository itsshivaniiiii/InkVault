using System;
using System.Threading.Tasks;
using InkVault.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace InkVault.Tests
{
    public class RegressionTests
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<AIEnhancementService>> _mockLogger;
        private readonly HttpClient _httpClient;

        public RegressionTests()
        {
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<AIEnhancementService>>();
            _httpClient = new HttpClient(); // For constructor only, won't make real calls in simple word-count tests
        }

        [Fact]
        public async Task AIEnhancementService_EmptyContent_ThrowsException()
        {
            // Arrange
            var service = new AIEnhancementService(_mockConfig.Object, _mockLogger.Object, _httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.EnhanceContentAsync(""));
        }

        [Theory]
        [InlineData("Too short", "Too short")] // 2 words
        [InlineData("Still not enough", "Still not enough")] // 3 words
        public async Task AIEnhancementService_ShortContent_ReturnsUnchanged(string content, string expected)
        {
            // Arrange
            var service = new AIEnhancementService(_mockConfig.Object, _mockLogger.Object, _httpClient);

            // Act
            var result = await service.EnhanceContentAsync(content);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task AIEnhancementService_MinimumMet_BypassesAPI_WithNoKey()
        {
            // Arrange
            _mockConfig.Setup(c => c["Gemini:ApiKey"]).Returns((string)null);
            var service = new AIEnhancementService(_mockConfig.Object, _mockLogger.Object, _httpClient);
            string content = "This sentence has enough words to trigger AI but no key.";

            // Act
            var result = await service.EnhanceContentAsync(content);

            // Assert
            // It should fall back to programmatic/null and return the same content (or programmatic enhanced if implemented)
            result.Should().NotBeNull();
        }
    }
}
