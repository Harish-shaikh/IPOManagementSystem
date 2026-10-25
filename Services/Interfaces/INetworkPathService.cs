namespace IPOProcessingSystem.Services.Interfaces;

public interface INetworkPathService
{
    bool IsNetworkPathAvailable { get; }
    string GetExportPath(int ipoId, string? clientName, string subFolder);
}
