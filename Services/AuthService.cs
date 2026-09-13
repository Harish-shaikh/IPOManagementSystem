using IPOProcessingSystem.Data;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

public class AuthService : IAuthService
{
    private readonly DatabaseService _dbService;
    public string? CurrentUsername { get; private set; }

    public AuthService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        // TODO: replace plain-text comparison with a proper password hash (e.g. BCrypt.Net)
        var user = await _dbService.Connection.Table<Models.User>()
            .Where(u => u.Username == username && u.PasswordHash == password && u.IsActive)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            CurrentUsername = user.Username;
            return true;
        }
        return false;
    }
}
