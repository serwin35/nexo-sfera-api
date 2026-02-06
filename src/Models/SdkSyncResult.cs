namespace NexoSferaApi.Models;

public enum SdkSyncStatus
{
    NotRun,
    AlreadyCurrent,
    Synchronized,
    PartialFailure,
    Failed,
    Skipped
}

public class SdkSyncResult
{
    public SdkSyncStatus Status { get; set; } = SdkSyncStatus.NotRun;
    public string? Message { get; set; }
    public string? LocalVersion { get; set; }
    public string? InstalledVersion { get; set; }
    public string? DetectedInstallPath { get; set; }
    public int FilesCopied { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
