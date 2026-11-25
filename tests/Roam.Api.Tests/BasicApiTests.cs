using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Roam.Api.Services;
using Roam.Domain.Users;
using Xunit;

namespace Roam.Api.Tests;

public class BasicApiTests
{
    [Fact]
    public async Task SmtpEmailSender_WhenHostNotConfigured_LogsFallbackWithoutThrowing()
    {
        var config = new ConfigurationBuilder().Build();
        var sender = new SmtpEmailSender(config, NullLogger<SmtpEmailSender>.Instance, new StubHostEnvironment(Environments.Development));
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };

        await sender.SendConfirmationLinkAsync(user, user.Email, "https://roam.local/confirm?code=abc");
        await sender.SendPasswordResetLinkAsync(user, user.Email, "https://roam.local/reset?code=def");
        await sender.SendPasswordResetCodeAsync(user, user.Email, "123456");

        Assert.Equal("user@example.com", user.Email);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SmtpEmailSender_WhenHostNotConfiguredOutsideDevelopment_DoesNotLogSecrets(string environmentName)
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new CapturingLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(config, logger, new StubHostEnvironment(environmentName));
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };

        await sender.SendConfirmationLinkAsync(user, user.Email, "https://roam.local/confirm?code=secret-confirm-token");
        await sender.SendPasswordResetLinkAsync(user, user.Email, "https://roam.local/reset?code=secret-reset-token");
        await sender.SendPasswordResetCodeAsync(user, user.Email, "987654");

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("secret-confirm-token"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("secret-reset-token"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("987654"));
    }

    [Fact]
    public async Task SmtpEmailSender_WhenHostNotConfiguredInDevelopment_LogsBodyForLocalTesting()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new CapturingLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(config, logger, new StubHostEnvironment(Environments.Development));
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };

        await sender.SendPasswordResetCodeAsync(user, user.Email, "987654");

        Assert.Contains(logger.Messages, m => m.Contains("987654"));
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Roam.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public void ApplicationUser_InitializesWithDefaultValues()
    {
        var user = new ApplicationUser
        {
            Email = "volunteer@example.com",
            DisplayName = "Explorer",
            VolunteerStatus = VolunteerStatus.Approved
        };

        Assert.Equal("Explorer", user.DisplayName);
        Assert.Equal(VolunteerStatus.Approved, user.VolunteerStatus);
    }
}
