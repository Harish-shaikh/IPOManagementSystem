using SQLite;

namespace IPOProcessingSystem.Models;

// Users & Auth
public class User
{
    [PrimaryKey, AutoIncrement]
    public int UserId { get; set; }
    [Unique]
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator"; // Admin / Operator / Viewer
    public bool IsActive { get; set; } = true;
}
