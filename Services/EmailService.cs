using System.Net;
using System.Net.Mail;
using System.Text.Json;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public class EmailService : IEmailService
{
    private readonly string _configPath;
    private EmailSettings? _settings;

    public EmailService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "IPOProcessingSystem");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "smtp.json");
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _settings = JsonSerializer.Deserialize<EmailSettings>(json);
            }
        }
        catch
        {
            _settings = null;
        }
    }

    public bool IsConfigured => _settings != null &&
                                !string.IsNullOrWhiteSpace(_settings.Host) &&
                                !string.IsNullOrWhiteSpace(_settings.FromAddress);

    public async Task<bool> SendEmailAsync(string to, string subject, string body, IEnumerable<string>? attachmentPaths = null)
    {
        return await SendEmailAsync(new[] { to }, subject, body, attachmentPaths);
    }

    public async Task<bool> SendEmailAsync(IEnumerable<string> toList, string subject, string body, IEnumerable<string>? attachmentPaths = null)
    {
        if (!IsConfigured || _settings == null)
        {
            return false;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            foreach (var recipient in toList)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                    message.To.Add(recipient.Trim());
            }

            if (message.To.Count == 0) return false;

            if (attachmentPaths != null)
            {
                foreach (var path in attachmentPaths)
                {
                    if (File.Exists(path))
                        message.Attachments.Add(new Attachment(path));
                }
            }

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmailService] Send error: {ex.Message}");
            return false;
        }
    }
}
