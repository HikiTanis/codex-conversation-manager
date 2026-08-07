using System;
using System.Collections.Generic;
using System.IO;

namespace CodexConversationMigrator;

internal static class BackupPackageFormat
{
	public const string ConversationExtension = ".codexchat";

	public const string ProjectExtension = ".codexproject";

	public const string LegacyPackExtension = ".codexpack";

	public const string RawBundleExtension = ".codexbundle";

	public static string ExtensionFor(bool includesProjectFiles)
	{
		return includesProjectFiles ? ProjectExtension : ConversationExtension;
	}

	public static bool IsFormalPackage(string path)
	{
		string extension = Path.GetExtension(path) ?? string.Empty;
		return string.Equals(extension, ConversationExtension, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(extension, ProjectExtension, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(extension, LegacyPackExtension, StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsSupportedImport(string path)
	{
		return IsFormalPackage(path) || string.Equals(Path.GetExtension(path), RawBundleExtension, StringComparison.OrdinalIgnoreCase);
	}

	public static string OpenDialogFilter => UiLanguage.T("Codex 正式备份 (*.codexchat;*.codexproject)|*.codexchat;*.codexproject|旧版备份 (*.codexpack;*.codexbundle)|*.codexpack;*.codexbundle|所有文件 (*.*)|*.*");
}

internal static class CctImportPathMapping
{
	public static bool AddArguments(List<string> args, string sourceProjectPath, string targetProjectPath, out string workDirectory)
	{
		if (args == null)
		{
			throw new ArgumentNullException(nameof(args));
		}
		workDirectory = null;
		if (string.IsNullOrWhiteSpace(targetProjectPath))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(sourceProjectPath))
		{
			if (string.Equals(TextHelpers.CanonicalPath(sourceProjectPath), TextHelpers.CanonicalPath(targetProjectPath), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			args.Add("--map-cwd");
			args.Add(sourceProjectPath + "=" + targetProjectPath);
			return true;
		}
		args.Add("--map-cwd-here");
		workDirectory = targetProjectPath;
		return true;
	}
}
