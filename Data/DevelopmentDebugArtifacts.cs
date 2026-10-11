using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace shopping_app.Data
{
	/// <summary>
	/// Keeps development-only logs and SQLite files together where they are
	/// easy to inspect without accidentally committing generated data.
	/// </summary>
	public static class DevelopmentDebugArtifacts
	{
		private const string FolderName = "DebugData";
		private static readonly object LogGate = new();

		/// <summary>
		/// Folder used for local Debug artifacts. On a normal Visual Studio run,
		/// walk up from the executable until the project file is found.
		/// </summary>
		public static string FolderPath
		{
			get
			{
				string? projectRoot =
					FindProjectRoot(Directory.GetCurrentDirectory())
					?? FindProjectRoot(AppContext.BaseDirectory);

				string folder = projectRoot is not null
					? Path.Combine(projectRoot, FolderName)
					: Path.Combine(FileSystem.AppDataDirectory, FolderName);

				Directory.CreateDirectory(folder);
				return folder;
			}
		}

		// SQLite automatically places its -wal and -shm sidecar files beside this file.
		public static string DatabasePath =>
			Path.Combine(FolderPath, "shopping.db3");

		public static string LogPath =>
			Path.Combine(FolderPath, "debug.log");

		/// <summary>
		/// Clear the previous application's log at the beginning of a Debug launch.
		/// Release builds do not create or modify this development log.
		/// </summary>
		public static void ResetLog()
		{
#if DEBUG
			ResetLogFile(LogPath);
#endif
		}

		/// <summary>
		/// Write a message to Visual Studio Output and the local debug.log file.
		/// Logging problems must never crash the application.
		/// </summary>
		public static void WriteLine(object? value)
		{
			string message = value?.ToString() ?? string.Empty;
			Debug.WriteLine(message);

#if DEBUG
			try
			{
				lock (LogGate)
				{
					Directory.CreateDirectory(FolderPath);
					File.AppendAllText(
						LogPath,
						$"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
				}
			}
			catch (Exception ex)
			{
				// The Visual Studio Output window remains available even if the
				// file cannot be written because of permissions or a file lock.
				Debug.WriteLine($"Could not write local debug log: {ex}");
			}
#endif
		}

		/// <summary>
		/// Find the repository root by walking upward until shopping-app.csproj
		/// is found. Returning null lets non-desktop platforms use app storage.
		/// </summary>
		public static string? FindProjectRoot(string startPath)
		{
			if (string.IsNullOrWhiteSpace(startPath))
			{
				return null;
			}

			var directory = new DirectoryInfo(startPath);

			while (directory is not null)
			{
				if (File.Exists(
					Path.Combine(directory.FullName, "shopping-app.csproj")))
				{
					return directory.FullName;
				}

				directory = directory.Parent;
			}

			return null;
		}

		/// <summary>
		/// Truncate one log file while preserving the file itself for inspection.
		/// This overload also makes the behavior easy to unit test safely.
		/// </summary>
		public static void ResetLogFile(string logPath)
		{
			if (string.IsNullOrWhiteSpace(logPath))
			{
				throw new ArgumentException(
					"A debug log path is required.",
					nameof(logPath));
			}

			string? directory = Path.GetDirectoryName(logPath);

			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			lock (LogGate)
			{
				File.WriteAllText(logPath, string.Empty);
			}
		}
	}
}
