using System;
using System.Linq;
using System.Threading.Tasks;
using InkVault.ViewModels;
using Xunit;

namespace InkVault.Tests
{
    public class ViewModelTests
    {
        [Theory]
        [InlineData("", 1)] // Empty content: 0 words -> Math.Max(1, 0) = 1
        [InlineData("<p>Hello world</p>", 1)] // 2 words -> Math.Max(1, 1) = 1
        [InlineData("Word1 Word2 Word3", 1)] // 3 words -> Math.Max(1, 1) = 1
        [InlineData("More words more words...", 1)]
        public void JournalStatItemViewModel_ReadingTimeMinutes_CalculatesCorrectly(string content, int expectedMinutes)
        {
            // Arrange
            var viewModel = new JournalStatItemViewModel
            {
                Content = content
            };

            // Act
            var result = viewModel.ReadingTimeMinutes;

            // Assert
            Assert.Equal(expectedMinutes, result);
        }

        [Fact]
        public void JournalStatItemViewModel_ReadingTimeMinutes_ManyWords_CalculatesCorrectly()
        {
            // Arrange
            // 201 words should be 2 minutes (Math.Ceiling(201/200.0) = 2)
            string content = string.Join(" ", Enumerable.Repeat("word", 201));
            var viewModel = new JournalStatItemViewModel
            {
                Content = content
            };

            // Act
            var result = viewModel.ReadingTimeMinutes;

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void JournalStatItemViewModel_ReadingTimeMinutes_WithHtml_StripsTags()
        {
            // Arrange
            // <p> and <b> are tags. 2 words "Hello" and "World"
            var viewModel = new JournalStatItemViewModel
            {
                Content = "<p>Hello <b>World</b></p>"
            };

            // Act
            var result = viewModel.ReadingTimeMinutes;

            // Assert
            Assert.Equal(1, result);
        }
    }
}
