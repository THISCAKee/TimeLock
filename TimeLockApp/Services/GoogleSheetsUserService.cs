using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimeLockApp.Configuration;
using TimeLockApp.Models;

namespace TimeLockApp.Services;

public sealed class GoogleSheetsUserService
{
    private SheetsService? _sheetsService;

    public async Task<IReadOnlyList<GoogleSheetUser>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        SheetsService service =
            await GetSheetsServiceAsync(cancellationToken);

        SpreadsheetsResource.ValuesResource.GetRequest request =
            service.Spreadsheets.Values.Get(
                GoogleSheetsConfig.SpreadsheetId,
                GoogleSheetsConfig.ReadRange);

        ValueRange response =
            await request.ExecuteAsync(cancellationToken);

        IList<IList<object>> rows =
            response.Values ?? new List<IList<object>>();

        var users = new List<GoogleSheetUser>();

        for (int index = 0; index < rows.Count; index++)
        {
            int sheetRowNumber = index + 2;

            GoogleSheetUser user =
                GoogleSheetUser.Parse(
                    rows[index],
                    sheetRowNumber);

            users.Add(user);
        }

        return users;
    }

    private async Task<SheetsService> GetSheetsServiceAsync(
        CancellationToken cancellationToken)
    {
        if (_sheetsService != null)
        {
            return _sheetsService;
        }

        string credentialPath =
            GoogleSheetsConfig.CredentialFilePath;

        if (!File.Exists(credentialPath))
        {
            throw new FileNotFoundException(
                "ไม่พบไฟล์ Service Account",
                credentialPath);
        }

        await using FileStream stream =
            File.OpenRead(credentialPath);

        GoogleCredential credential =
            GoogleCredential
                .FromStream(stream)
                .CreateScoped(
                    SheetsService.Scope.SpreadsheetsReadonly);

        cancellationToken.ThrowIfCancellationRequested();

        _sheetsService = new SheetsService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "TimeLockApp"
            });

        return _sheetsService;
    }
}