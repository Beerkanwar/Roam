using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Roam.Api.Services;
using Roam.Domain.Users;
using Xunit;

namespace Roam.Api.Tests;

public class BasicApiTests
{
    [Fact]
    public async Task LoggingEmailSender_ProcessesConfirmationAndResetLinks()
    {
        var sender = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };

        await sender.SendConfirmationLinkAsync(user, user.Email, "https://roam.local/confirm?code=abc");
        await sender.SendPasswordResetLinkAsync(user, user.Email, "https://roam.local/reset?code=def");
        await sender.SendPasswordResetCodeAsync(user, user.Email, "123456");

        Assert.Equal("user@example.com", user.Email);
    }
}
