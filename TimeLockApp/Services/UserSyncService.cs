using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimeLockApp.Data;
using TimeLockApp.Models;

namespace TimeLockApp.Services;

public sealed class UserSyncService
{
    private readonly GoogleSheetsUserService _googleSheetsUserService;
    private readonly DatabaseService _databaseService;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public UserSyncService(
        GoogleSheetsUserService googleSheetsUserService,
        DatabaseService databaseService)
    {
        _googleSheetsUserService = googleSheetsUserService;
        _databaseService = databaseService;
    }

    public async Task<UserSyncResult> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);

        try
        {
            await ProcessPendingDeactivationsCoreAsync(
                cancellationToken);

            IReadOnlyList<GoogleSheetUser> users =
                await _googleSheetsUserService.GetUsersAsync(
                    cancellationToken);

            _databaseService.SynchronizeUsers(users);

            return UserSyncResult.Success(users.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UserSyncResult.Failure(ex.Message);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task ProcessPendingDeactivationsAsync(
        CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);

        try
        {
            await ProcessPendingDeactivationsCoreAsync(
                cancellationToken);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task ProcessPendingDeactivationsCoreAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingUserDeactivation> pendingUsers =
            _databaseService.GetPendingUserDeactivations();

        foreach (PendingUserDeactivation pendingUser in pendingUsers)
        {
            try
            {
                bool rowFound =
                    await _googleSheetsUserService.SetUserActiveAsync(
                        pendingUser.ExternalUserId,
                        false,
                        cancellationToken);

                if (!rowFound)
                {
                    Debug.WriteLine(
                        "ไม่พบ Google Sheet row สำหรับ " +
                        $"UserId {pendingUser.ExternalUserId}");

                    continue;
                }

                _databaseService.MarkUserDeactivationSynchronized(
                    pendingUser.LocalUserId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "เขียนสถานะ FALSE ไป Google Sheet ไม่สำเร็จ " +
                    $"(LocalId={pendingUser.LocalUserId}, " +
                    $"UserId={pendingUser.ExternalUserId}): " +
                    ex.Message);
            }
        }
    }
}

public sealed class UserSyncResult
{
    public bool IsSuccessful { get; private init; }

    public int UserCount { get; private init; }

    public string ErrorMessage { get; private init; } = "";

    public static UserSyncResult Success(int userCount)
    {
        return new UserSyncResult
        {
            IsSuccessful = true,
            UserCount = userCount
        };
    }

    public static UserSyncResult Failure(string errorMessage)
    {
        return new UserSyncResult
        {
            IsSuccessful = false,
            ErrorMessage = errorMessage
        };
    }
}
