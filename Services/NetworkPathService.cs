using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

public class NetworkPathService : INetworkPathService
{
    private const string UncBase = @"\\192.168.0.253\IDRIVE\ISSUE\EDP";

    public bool IsNetworkPathAvailable
    {
        get
        {
            try
            {
                return Directory.Exists(UncBase);
            }
            catch
            {
                return false;
            }
        }
    }

    public string GetExportPath(int ipoId, string? clientName, string subFolder)
    {
        string safeClient = string.IsNullOrWhiteSpace(clientName) ? $"IPO_{ipoId}" : SanitizePath(clientName);
        string targetDir;

        if (IsNetworkPathAvailable)
        {
            targetDir = Path.Combine(UncBase, safeClient, subFolder);
        }
        else
        {
            var localBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Exports", $"IPO_{ipoId}");
            targetDir = Path.Combine(localBase, subFolder);
        }

        try
        {
            Directory.CreateDirectory(targetDir);
        }
        catch
        {
            // Fallback to local app data if network permissions fail
            var fallback = Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{ipoId}", subFolder);
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        return targetDir;
    }

    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
