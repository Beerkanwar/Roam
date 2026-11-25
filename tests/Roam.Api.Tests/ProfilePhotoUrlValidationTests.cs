using Roam.Api.Endpoints;
using Xunit;

namespace Roam.Api.Tests;

public class ProfilePhotoUrlValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://cdn.example.com/avatars/123.png")]
    [InlineData("  https://cdn.example.com/a.jpg  ")]
    public void AcceptsAbsentOrHttpsUrls(string? url)
    {
        Assert.True(UserProfileEndpoints.IsValidProfilePhotoUrl(url));
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+")]
    [InlineData("http://cdn.example.com/a.png")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("//cdn.example.com/a.png")]
    [InlineData("/relative/path.png")]
    [InlineData("https://user:pass@example.com/a.png")]
    [InlineData("not a url")]
    public void RejectsUnsafeOrNonHttpsUrls(string url)
    {
        Assert.False(UserProfileEndpoints.IsValidProfilePhotoUrl(url));
    }

    [Fact]
    public void RejectsOverlongUrls()
    {
        var url = "https://cdn.example.com/" + new string('a', UserProfileEndpoints.MaxProfilePhotoUrlLength);

        Assert.False(UserProfileEndpoints.IsValidProfilePhotoUrl(url));
    }
}
