using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TimeLockApp.Services;

public sealed class ChromeLaunchResult
{
    public bool IsSuccessful { get; private init; }
    public string ErrorMessage { get; private init; } = "";

    public static ChromeLaunchResult Success() =>
        new() { IsSuccessful = true };

    public static ChromeLaunchResult Failure(string message) =>
        new() { ErrorMessage = message };
}

public sealed class ChromeLauncherService
{
    private const string ChromeAppPathKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";

    public ChromeLaunchResult TryOpen(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return ChromeLaunchResult.Failure(
                "URL เว็บไซต์ไม่ถูกต้อง");
        }

        string? chromePath = FindChromePath();

        if (chromePath == null)
        {
            return ChromeLaunchResult.Failure(
                "ไม่พบ Google Chrome ในเครื่องนี้");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = chromePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add(url);

            Process? process = Process.Start(startInfo);

            return process == null
                ? ChromeLaunchResult.Failure(
                    "ไม่สามารถเปิด Google Chrome ได้")
                : ChromeLaunchResult.Success();
        }
        catch (Exception)
        {
            return ChromeLaunchResult.Failure(
                "ไม่สามารถเปิด Google Chrome ได้");
        }
    }

    private static string? FindChromePath()
    {
        var candidates = new List<string>();

        AddRegistryCandidate(
            candidates,
            RegistryHive.CurrentUser,
            RegistryView.Registry64);
        AddRegistryCandidate(
            candidates,
            RegistryHive.CurrentUser,
            RegistryView.Registry32);
        AddRegistryCandidate(
            candidates,
            RegistryHive.LocalMachine,
            RegistryView.Registry64);
        AddRegistryCandidate(
            candidates,
            RegistryHive.LocalMachine,
            RegistryView.Registry32);

        AddKnownFolderCandidate(
            candidates,
            Environment.SpecialFolder.ProgramFiles);
        AddKnownFolderCandidate(
            candidates,
            Environment.SpecialFolder.ProgramFilesX86);
        AddKnownFolderCandidate(
            candidates,
            Environment.SpecialFolder.LocalApplicationData);

        var inspectedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidates)
        {
            string normalizedPath = candidate.Trim().Trim('"');

            if (normalizedPath.Length > 0 &&
                inspectedPaths.Add(normalizedPath) &&
                File.Exists(normalizedPath))
            {
                return normalizedPath;
            }
        }

        return null;
    }

    private static void AddRegistryCandidate(
        List<string> candidates,
        RegistryHive hive,
        RegistryView view)
    {
        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? appPathKey =
                baseKey.OpenSubKey(ChromeAppPathKey);

            if (appPathKey?.GetValue(null) is string path &&
                !string.IsNullOrWhiteSpace(path))
            {
                candidates.Add(path);
            }
        }
        catch (Exception)
        {
            // Try the remaining registry views and standard locations.
        }
    }

    private static void AddKnownFolderCandidate(
        List<string> candidates,
        Environment.SpecialFolder folder)
    {
        string basePath = Environment.GetFolderPath(folder);

        if (!string.IsNullOrWhiteSpace(basePath))
        {
            candidates.Add(Path.Combine(
                basePath,
                "Google",
                "Chrome",
                "Application",
                "chrome.exe"));
        }
    }
}
