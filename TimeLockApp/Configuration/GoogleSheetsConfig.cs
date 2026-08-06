using System;
using System.IO;

namespace TimeLockApp.Configuration;

public static class GoogleSheetsConfig
{
    public const string SpreadsheetId =
        "1gR_FJjZpabhfxf_5GPHFfJ9U89Fu_D5R5DZnS4K1guA";

    public const string WorksheetName = "Users";

    public static string ReadRange =>
        $"{WorksheetName}!A2:F";

    public static string CredentialFilePath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Secrets",
            "service-account.json");
}