namespace CodexConversationManager;

internal sealed class ProjectPayloadInfo
{
	public string archive_file { get; set; }

	public string source_path { get; set; }

	public string root_name { get; set; }

	public int file_count { get; set; }

	public int directory_count { get; set; }

	public int skipped_reparse_points { get; set; }

	public long uncompressed_bytes { get; set; }

	public string sha256 { get; set; }
}

internal enum ProjectFileConflictMode
{
	RequireEmpty,
	SkipExisting,
	OverwriteWithBackup
}

internal sealed class ProjectRestorePlan
{
	public string TargetPath { get; set; }

	public int FileCount { get; set; }

	public int DirectoryCount { get; set; }

	public int ExistingFileCount { get; set; }

	public int NewFileCount { get; set; }

	public long UncompressedBytes { get; set; }

	public ProjectFileConflictMode ConflictMode { get; set; }
}

internal sealed class ProjectRestoreResult
{
	public string TargetPath { get; set; }

	public int CreatedFileCount { get; set; }

	public int OverwrittenFileCount { get; set; }

	public int SkippedFileCount { get; set; }

	public string BackupPath { get; set; }
}
