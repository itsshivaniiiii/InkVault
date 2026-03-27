using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;
using System.Net;
using System.Threading.Tasks;

namespace InkVault.Tests
{
    public class BasicIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public BasicIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Account/Login")]
        [InlineData("/Account/Register")]
        public async Task Get_Endpoints_ReturnSuccessAndCorrectContentType(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.ToString().Should().Contain("text/html");
        }

        [Fact]
        public async Task LoginEncodedPage_ContainsInkVaultTitle()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/Account/Login");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            content.Should().Contain("<title>");
            content.Should().Contain("InkVault");
        }
    }
}
