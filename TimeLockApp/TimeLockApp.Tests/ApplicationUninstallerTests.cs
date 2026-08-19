using TimeLockApp.Services;

static class ApplicationUninstallerTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("finds the installed Inno Setup uninstaller", FindsInstalledUninstaller);
        yield return ("returns no uninstaller when the file is missing", ReturnsNoUninstallerWhenMissing);
        yield return ("installer generates a hidden uninstaller", InstallerGeneratesAHiddenUninstaller);
        yield return ("admin window exposes the uninstall action", AdminWindowExposesTheUninstallAction);
        yield return ("main window does not expose uninstall text", MainWindowDoesNotExposeUninstallText);
    }

    private static void FindsInstalledUninstaller()
    {
        using var fixture = new UninstallerFixture();
        File.WriteAllText(fixture.UninstallerPath, string.Empty);

        string? result = ApplicationUninstaller.FindUninstaller(fixture.DirectoryPath);

        AssertTrue(
            result == fixture.UninstallerPath,
            "The installed uninstaller path should be returned.");
    }

    private static void ReturnsNoUninstallerWhenMissing()
    {
        using var fixture = new UninstallerFixture();

        string? result = ApplicationUninstaller.FindUninstaller(fixture.DirectoryPath);

        AssertTrue(result is null, "A missing uninstaller should return null.");
    }

    private static void InstallerGeneratesAHiddenUninstaller()
    {
        string installer = ReadRepositoryFile("deployment", "installer", "TimeLock.iss");

        AssertTrue(
            installer.Contains("Uninstallable=yes", StringComparison.Ordinal),
            "The installer must generate unins000.exe for the Admin Panel.");
        AssertTrue(
            installer.Contains("CreateUninstallRegKey=no", StringComparison.Ordinal),
            "The installer must not expose an Apps & Features uninstall entry.");
        AssertTrue(
            installer.Contains("UninstallFilesDir={app}\\.uninstall", StringComparison.Ordinal),
            "The installer must place uninstaller files in the hidden .uninstall directory.");
        AssertTrue(
            installer.Contains("Attribs: hidden", StringComparison.Ordinal),
            "The installer must mark the uninstaller directory as hidden.");
    }

    private static void AdminWindowExposesTheUninstallAction()
    {
        string adminWindow = ReadRepositoryFile("AdminWindow.xaml");

        AssertTrue(
            adminWindow.Contains("Click=\"UninstallButton_Click\"", StringComparison.Ordinal),
            "The admin window must expose the uninstall action.");
    }

    private static void MainWindowDoesNotExposeUninstallText()
    {
        string mainWindow = ReadRepositoryFile("MainWindow.xaml");

        AssertTrue(
            !mainWindow.Contains("Uninstall", StringComparison.OrdinalIgnoreCase),
            "The main window must not contain uninstall text.");
    }

    private static string ReadRepositoryFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "TimeLockApp.Tests",
                   "TimeLockApp.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the application repository root from the test output directory.");
        }

        return File.ReadAllText(Path.Combine(
            new[] { directory.FullName }.Concat(relativeParts).ToArray()));
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class UninstallerFixture : IDisposable
    {
        public UninstallerFixture()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "TimeLockApp.Tests",
                "Uninstaller",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            Directory.CreateDirectory(Path.Combine(DirectoryPath, ".uninstall"));
        }

        public string DirectoryPath { get; }

        public string UninstallerPath =>
            Path.Combine(DirectoryPath, ".uninstall", "unins000.exe");

        public void Dispose()
        {
            string resolvedDirectory = Path.GetFullPath(DirectoryPath);
            string allowedRoot = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "TimeLockApp.Tests", "Uninstaller"));

            if (!resolvedDirectory.StartsWith(
                    allowedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Refusing to remove a directory outside the test root.");
            }

            Directory.Delete(resolvedDirectory, recursive: true);
        }
    }
}
