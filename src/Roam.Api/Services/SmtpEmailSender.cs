using System;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Roam.Domain.Users;

namespace Roam.Api.Services;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger, IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    // ASP.NET Core Identity passes links that are already HTML-encoded (e.g. "&amp;" between query
    // parameters), so the emails are HTML: a plain-text body would show a link that no longer works.
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your Roam account email";
        var body = Layout("Confirm your email",
            $"<p>Welcome to Roam! Please confirm your email address so you can reset your password if you ever forget it.</p>" +
            $"<p><a href=\"{confirmationLink}\">Confirm my email</a></p>" +
            $"<p>If you did not create a Roam account, you can ignore this email.</p>");
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset your Roam password";
        var body = Layout("Reset your password",
            $"<p>You requested a password reset.</p><p><a href=\"{resetLink}\">Reset my password</a></p>" +
            $"<p>If you did not ask for this, you can ignore this email.</p>");
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Your Roam password reset code";
        // Identity's reset codes are long, so also send a link that opens Roam with the code filled in.
        // The values go in the URL fragment (#...), which browsers never send to the server or its logs.
        var publicUrl = _configuration["App:PublicUrl"]?.TrimEnd('/');
        var linkHtml = string.IsNullOrWhiteSpace(publicUrl)
            ? string.Empty
            : $"<p><a href=\"{HtmlEncoder.Default.Encode($"{publicUrl}/#reset-password?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(resetCode)}")}\">Choose a new password</a></p>" +
              "<p>Or enter this code in Roam:</p>";
        var body = Layout("Reset your password",
            (linkHtml.Length > 0 ? linkHtml : "<p>Enter this code in Roam to choose a new password:</p>") +
            $"<p style=\"font-family:monospace;font-size:15px;word-break:break-all;background:#f1f5f9;padding:10px;border-radius:6px\">{HtmlEncoder.Default.Encode(resetCode)}</p>" +
            $"<p>If you did not ask for this, you can ignore this email.</p>");
        await SendEmailAsync(email, subject, body);
    }

    private static string Layout(string heading, string content) =>
        $"<div style=\"font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:520px;margin:0 auto;color:#0f172a\">" +
        $"<h2 style=\"color:#4f46e5\">{HtmlEncoder.Default.Encode(heading)}</h2>{content}" +
        $"<p style=\"color:#64748b;font-size:12px\">Roam - human-powered exploration</p></div>";

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // The body contains confirmation links and password-reset codes, which are bearer secrets.
            // Only echo it to the log for local development; elsewhere, record that delivery was skipped.
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("SMTP host not configured. Development fallback log for {Email} [{Subject}]: {Body}", toEmail, subject, body);
            }
            else
            {
                _logger.LogWarning("SMTP host not configured; email '{Subject}' was not delivered.", subject);
            }
            return;
        }

        var portStr = _configuration["Smtp:Port"];
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 587;

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? "noreply@roam.local";
        var fromName = _configuration["Smtp:FromName"] ?? "Roam";
        // STARTTLS by default; only a local mail catcher should ever turn this off.
        var enableSsl = !string.Equals(_configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var message = new MailMessage(new MailAddress(fromEmail, fromName), new MailAddress(toEmail))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email '{Subject}' sent.", subject);
        }
        catch (Exception ex)
        {
            // Do not fail the caller: registration has already created the account, and the password-reset
            // endpoint must answer the same way whether or not mail could be sent.
            _logger.LogError(ex, "Failed to send email '{Subject}' via SMTP host {Host}", subject, host);
        }
    }
}

