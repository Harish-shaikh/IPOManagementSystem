using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public interface IBendemService
{
    /// <summary>Generate CDSL bendem file with the given sequence number.</summary>
    Task<string> GenerateCdslBendemAsync(int ipoId, string sequenceNo, string exportPath);
    /// <summary>Generate NSDL bendem file (auto-sequence).</summary>
    Task<string> GenerateNsdlBendemAsync(int ipoId, string exportPath);
    /// <summary>Upload and verify CDSL bendem file.</summary>
    Task<bool> VerifyCdslBendemAsync(int ipoId, string verifiedFilePath);
    /// <summary>Upload and verify NSDL bendem file (strips last 2 footer lines per spec).</summary>
    Task<bool> VerifyNsdlBendemAsync(int ipoId, string verifiedFilePath);
    /// <summary>Merge bendem files and produce Validation XLS, Bid Error Data XLS, Book Data XLS.</summary>
    Task<List<string>> MergeBendemAsync(int ipoId, string exportPath);
    Task<List<BendemFile>> GetBendemFilesAsync(int ipoId);
    Task DeleteVerificationAsync(int ipoId, string depository);
}
