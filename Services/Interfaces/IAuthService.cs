namespace IPOProcessingSystem.Services.Interfaces;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    string? CurrentUsername { get; }
}
