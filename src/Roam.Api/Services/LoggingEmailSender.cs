using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Roam.Domain.Users;

namespace Roam.Api.Services;

public class LoggingEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        _logger.LogInformation("Sending email confirmation link to {Email}: {ConfirmationLink}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        _logger.LogInformation("Sending password reset link to {Email}: {ResetLink}", email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        _logger.LogInformation("Sending password reset code to {Email}: {ResetCode}", email, resetCode);
        return Task.CompletedTask;
    }
}
