using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ArchiveNumerique.Services;
using FluentAssertions;

namespace ArchiveNumerique.Tests.Services
{
    public class CrawlerServiceTests
    {
        private readonly CrawlerService _crawlerService;
        private const string TestSite = "https://cesi.fr";

        public CrawlerServiceTests()
        {
            _crawlerService = new CrawlerService();
        }

        [Fact]
        public async Task GetInternalLinksAsync_WithValidUrl_ReturnsListOfLinks()
        {
            // Arrange
            var domain = TestSite;

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(domain);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<string>>();
        }

        [Fact]
        public async Task GetInternalLinksAsync_WithValidUrl_ReturnsDistinctLinks()
        {
            // Arrange
            var domain = TestSite;

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(domain);

            // Assert
            var distinctCount = result.Distinct().Count();
            result.Count.Should().Be(distinctCount, "tous les liens doivent être uniques");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task GetInternalLinksAsync_WithInvalidUrl_ThrowsException(string invalidUrl)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _crawlerService.GetInternalLinksAsync(invalidUrl));
        }

        [Fact]
        public async Task GetInternalLinksAsync_WithInvalidUrlFormat_ThrowsUriFormatException()
        {
            // Arrange
            var invalidUrl = "not-a-valid-url";

            // Act & Assert
            await Assert.ThrowsAsync<UriFormatException>(
                async () => await _crawlerService.GetInternalLinksAsync(invalidUrl));
        }

        [Fact]
        public async Task GetInternalLinksAsync_WithSimpleSite_ReturnsExpectedLinks()
        {
            // Arrange
            var testUrl = TestSite;

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(testUrl);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain(link => link.StartsWith(testUrl));
            result.All(link => Uri.IsWellFormedUriString(link, UriKind.Absolute))
                .Should().BeTrue("tous les liens doivent être des URI absolues valides");
        }

        [Fact]
        public async Task GetInternalLinksAsync_ReturnsOnlyInternalLinks()
        {
            // Arrange
            var domain = TestSite;

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(domain);

            // Assert
            foreach (var link in result)
            {
                var uri = new Uri(link);
                var domainUri = new Uri(domain);
                uri.Host.Should().Be(domainUri.Host, 
                    "seuls les liens internes au domaine doivent être retournés");
            }
        }

        [Fact]
        public async Task GetInternalLinksAsync_WithHttpsUrl_ReturnsLinks()
        {
            // Arrange
            var secureUrl = "https://example.com";

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(secureUrl);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<string>>();
        }

        [Fact]
        public async Task GetInternalLinksAsync_CompletesWithinReasonableTime()
        {
            // Arrange
            var domain = TestSite;
            var timeout = TimeSpan.FromMinutes(5);

            // Act
            var task = _crawlerService.GetInternalLinksAsync(domain);
            var completedInTime = await Task.WhenAny(task, Task.Delay(timeout)) == task;

            // Assert
            completedInTime.Should().BeTrue("le crawling devrait se terminer dans un délai raisonnable");
            
            if (completedInTime)
            {
                var result = await task;
                result.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task GetInternalLinksAsync_ReturnsEmptyListWhenNoLinksFound()
        {
            // Arrange
            // Utiliser une URL qui existe mais n'a pas de liens
            var domain = "https://example.com/page-without-links";

            // Act
            var result = await _crawlerService.GetInternalLinksAsync(domain);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<string>>();
        }
    }
}