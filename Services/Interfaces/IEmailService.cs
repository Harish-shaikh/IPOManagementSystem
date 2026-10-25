namespace IPOProcessingSystem.Services.Interfaces;

public interface IEmailService
{
    bool IsConfigured { get; }
    Task<bool> SendEmailAsync(string to, string subject, string body, IEnumerable<string>? attachmentPaths = null);
    Task<bool> SendEmailAsync(IEnumerable<string> toList, string subject, string body, IEnumerable<string>? attachmentPaths = null);
}
