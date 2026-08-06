using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
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

    public async Task<bool> SetUserActiveAsync(
        int externalUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        SheetsService service =
            await GetSheetsServiceAsync(cancellationToken);

        string userIdRange =
            $"{GoogleSheetsConfig.WorksheetName}!A2:A";

        SpreadsheetsResource.ValuesResource.GetRequest getRequest =
            service.Spreadsheets.Values.Get(
                GoogleSheetsConfig.SpreadsheetId,
                userIdRange);

        ValueRange response =
            await getRequest.ExecuteAsync(cancellationToken);

        IList<IList<object>> rows =
            response.Values ?? new List<IList<object>>();

        int sheetRow = 0;

        for (int index = 0; index < rows.Count; index++)
        {
            IList<object> row = rows[index];

            if (row.Count == 0)
            {
                continue;
            }

            string userIdText = Convert.ToString(
                row[0],
                CultureInfo.InvariantCulture) ?? "";

            if (int.TryParse(
                    userIdText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int rowUserId) &&
                rowUserId == externalUserId)
            {
                sheetRow = index + 2;
                break;
            }
        }

        if (sheetRow == 0)
        {
            return false;
        }

        var valueRange = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object> { isActive }
            }
        };

        string updateRange =
            $"{GoogleSheetsConfig.WorksheetName}!F{sheetRow}";

        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest =
            service.Spreadsheets.Values.Update(
                valueRange,
                GoogleSheetsConfig.SpreadsheetId,
                updateRange);

        updateRequest.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest
                .ValueInputOptionEnum.RAW;

        await updateRequest.ExecuteAsync(cancellationToken);
        return true;
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
                    SheetsService.Scope.Spreadsheets);

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
