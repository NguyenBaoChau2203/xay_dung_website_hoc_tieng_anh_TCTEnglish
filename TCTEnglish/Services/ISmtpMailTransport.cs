using System.Net;
using System.Net.Mail;

namespace TCTVocabulary.Services;

/// <summary>
/// Seam over SMTP delivery so tests can fake stall/failure/cancel without live mail.
/// </summary>
public interface ISmtpMailTransport
{
    Task SendAsync(
        string host,
        int port,
        string username,
        string password,
        MailMessage message,
        CancellationToken cancellationToken);
}

public sealed class SmtpClientMailTransport : ISmtpMailTransport
{
    public async Task SendAsync(
        string host,
        int port,
        string username,
        string password,
        MailMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(message);

        using var smtpClient = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

        await smtpClient.SendMailAsync(message, cancellationToken);
    }
}
