using shopping_app.Data;

namespace ShoppingApp.Tests
{
	public class DevelopmentDebugArtifactsTests
	{
		[Fact]
		public void FindProjectRoot_FromNestedBuildFolder_FindsProjectDirectory()
		{
			// Debug builds run several folders below the repository root. Walking
			// upward should stop at the folder that contains shopping-app.csproj.
			string root = Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-debug-root-{Guid.NewGuid():N}");

			string nested = Path.Combine(
				root,
				"bin",
				"Debug",
				"net10.0-windows10.0.19041.0",
				"win-x64");

			Directory.CreateDirectory(nested);
			File.WriteAllText(
				Path.Combine(root, "shopping-app.csproj"),
				"<Project />");

			try
			{
				string? result =
					DevelopmentDebugArtifacts.FindProjectRoot(nested);

				Assert.Equal(root, result);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void ResetLogFile_WithOldOutput_LeavesFreshEmptyLog()
		{
			// Each application launch should discard yesterday's debugging noise
			// so the file contains only information from the current run.
			string folder = Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-debug-log-{Guid.NewGuid():N}");

			string logPath = Path.Combine(folder, "debug.log");
			Directory.CreateDirectory(folder);
			File.WriteAllText(logPath, "old debug output");

			try
			{
				DevelopmentDebugArtifacts.ResetLogFile(logPath);

				Assert.True(File.Exists(logPath));
				Assert.Equal(string.Empty, File.ReadAllText(logPath));
			}
			finally
			{
				Directory.Delete(folder, recursive: true);
			}
		}
	}
}
