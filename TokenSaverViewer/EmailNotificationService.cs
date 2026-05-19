using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TokenSaverViewer;

public sealed class EmailNotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendNewClientNotificationAsync(string clientId)
    {
        var host = _config["Email:SmtpHost"];
        var portStr = _config["Email:SmtpPort"];
        var address = _config["Email:Address"];
        var password = _config["Email:Password"];
        var recipient = _config["Email:Recipient"] ?? address;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Email not configured — skipping new-client notification for {ClientId}", clientId);
            return;
        }

        var port = int.TryParse(portStr, out var p) ? p : 587;
        var resolvedRecipient = string.IsNullOrWhiteSpace(recipient) ? address : recipient;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(address));
        message.To.Add(MailboxAddress.Parse(resolvedRecipient));
        message.Subject = "TokenSaver — new client registered";
        message.Body = new TextPart("plain")
        {
            Text = $"A new client has connected to TokenSaver.\n\nClient ID: {clientId}\nTime (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n"
        };

        try
        {
            using var protocolLog = new MailKit.ProtocolLogger(Console.OpenStandardError(), leaveOpen: true);
            using var smtp = new SmtpClient(protocolLog);
            smtp.ServerCertificateValidationCallback = AcceptRevocationFailures;
            var ssl = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await smtp.ConnectAsync(host, port, ssl);
            await smtp.AuthenticateAsync(address, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
            _logger.LogInformation("New-client notification sent for {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new-client notification for {ClientId}", clientId);
        }
    }

    private static bool AcceptRevocationFailures(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        // Accept if the only chain errors are revocation-related (CRL endpoint unreachable).
        if (errors == SslPolicyErrors.RemoteCertificateChainErrors && chain is not null)
        {
            var onlyRevocation = chain.ChainStatus.All(s =>
                s.Status is X509ChainStatusFlags.RevocationStatusUnknown
                         or X509ChainStatusFlags.OfflineRevocation
                         or X509ChainStatusFlags.NoError);
            return onlyRevocation;
        }

        return false;
    }
}
