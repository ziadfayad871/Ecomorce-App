using System.Net;
using System.Net.Mail;
using Core.Application.Common.Communication;
using DataAccess.Options;
using Microsoft.Extensions.Options;

namespace DataAccess.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly string _fromEmail;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
        _fromEmail = string.IsNullOrWhiteSpace(_options.FromEmail)
            ? _options.UserName?.Trim() ?? string.Empty
            : _options.FromEmail.Trim();
    }

    public async System.Threading.Tasks.Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_fromEmail))
        {
            throw new InvalidOperationException("SMTP settings are not configured.");
        }

        using var message = new MailMessage
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };

        message.To.Add(new MailAddress(toEmail));
        message.From = string.IsNullOrWhiteSpace(_options.FromDisplayName)
            ? new MailAddress(_fromEmail)
            : new MailAddress(_fromEmail, _options.FromDisplayName);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Password))
        {
            client.Credentials = new NetworkCredential(_fromEmail, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
