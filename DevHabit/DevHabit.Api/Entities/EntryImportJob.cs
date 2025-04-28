namespace DevHabit.Api.Entities;

public sealed class EntryImportJob
{
    public string Id { get; set; }
    public string UserId { get; set; } // User who uploaded the file
    public EntryImportStatus Status { get; set; } // Status of the import job
    public string FileName { get; set; } 
    public byte[] FileContent { get; set; } //上传的文件内容（二进制数组，byte[])
    public int TotalRecords { get; set; } // Total number of records in the file
    public int ProcessedRecords { get; set; } // Number of records processed so far
    public int SuccessfulRecords { get; set; } // Number of records successfully imported
    public int FailedRecords { get; set; } // Number of records that failed to import
    public List<string> Errors { get; set; } = []; // List of errors encountered during import
    public DateTime CreatedAtUtc { get; set; } //   Creation time of the job
    public DateTime? CompletedAtUtc { get; set; } // Completion time of the job (if applicable)

    public static string NewId()
    {
        return $"ei_{Guid.CreateVersion7()}";
    }
}

public enum EntryImportStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
