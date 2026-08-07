using TimeLockApp.Models;
using TimeLockApp.Services;

internal static class UserSyncServiceTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "user sync propagates material change state",
            () => PropagatesMaterialChangeStateAsync().GetAwaiter().GetResult());
        yield return (
            "user sync serializes concurrent sheet access",
            () => SerializesConcurrentAccessAsync().GetAwaiter().GetResult());
        yield return (
            "user sync failure preserves local users",
            () => FailurePreservesLocalUsersAsync().GetAwaiter().GetResult());
    }

    private static async Task PropagatesMaterialChangeStateAsync()
    {
        using var fixture = new TestDatabase();
        var sheet = new FakeGoogleSheetsUserService
        {
            Users = new[] { SheetUser(601, "sync-user") }
        };
        var service = new UserSyncService(sheet, fixture.Service);

        UserSyncResult first = await service.SynchronizeAsync();
        UserSyncResult second = await service.SynchronizeAsync();

        AssertTrue(
            first.IsSuccessful && first.HasChanges,
            "The first imported row must report changes.");
        AssertTrue(
            second.IsSuccessful && !second.HasChanges,
            "An identical import must report no changes.");
    }

    private static async Task SerializesConcurrentAccessAsync()
    {
        using var fixture = new TestDatabase();
        var sheet = new FakeGoogleSheetsUserService
        {
            Users = new[] { SheetUser(602, "serialized-user") },
            PauseFirstRead = true
        };
        var service = new UserSyncService(sheet, fixture.Service);

        Task<UserSyncResult> first = service.SynchronizeAsync();
        await sheet.FirstReadStarted.Task;

        Task<UserSyncResult> second = service.SynchronizeAsync();
        await Task.Yield();

        AssertTrue(
            sheet.ReadCalls == 1,
            "The second Sheet read must wait for the first one.");

        sheet.ReleaseFirstRead.TrySetResult();
        await Task.WhenAll(first, second);

        AssertTrue(
            sheet.MaximumConcurrentReads == 1,
            "Sheet reads must never overlap.");
    }

    private static async Task FailurePreservesLocalUsersAsync()
    {
        using var fixture = new TestDatabase();
        fixture.Service.SynchronizeUsers(
            new[] { SheetUser(603, "preserved-user") });

        var sheet = new FakeGoogleSheetsUserService
        {
            ReadException = new InvalidOperationException("network unavailable")
        };
        var service = new UserSyncService(sheet, fixture.Service);

        UserSyncResult result = await service.SynchronizeAsync();

        AssertTrue(!result.IsSuccessful, "The failed read must return failure.");
        AssertTrue(
            result.ErrorMessage.Contains("network unavailable", StringComparison.Ordinal),
            "The failure must contain the source error.");
        AssertTrue(
            fixture.Service.GetAllUsers().Any(user => user.Username == "preserved-user"),
            "A failed read must preserve committed local users.");
    }

    private static GoogleSheetUser SheetUser(int id, string username)
    {
        return new GoogleSheetUser
        {
            UserId = id,
            Username = username,
            Password = "password",
            AllowedMinutes = 10,
            Role = "user",
            IsActive = true
        };
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeGoogleSheetsUserService : IGoogleSheetsUserService
    {
        private int _activeReads;
        private int _maximumConcurrentReads;
        private int _readCalls;

        public IReadOnlyList<GoogleSheetUser> Users { get; init; } =
            Array.Empty<GoogleSheetUser>();

        public Exception? ReadException { get; init; }

        public bool PauseFirstRead { get; init; }

        public TaskCompletionSource FirstReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadCalls => Volatile.Read(ref _readCalls);

        public int MaximumConcurrentReads =>
            Volatile.Read(ref _maximumConcurrentReads);

        public async Task<IReadOnlyList<GoogleSheetUser>> GetUsersAsync(
            CancellationToken cancellationToken = default)
        {
            int call = Interlocked.Increment(ref _readCalls);
            int active = Interlocked.Increment(ref _activeReads);
            UpdateMaximum(active);

            try
            {
                if (PauseFirstRead && call == 1)
                {
                    FirstReadStarted.TrySetResult();
                    await ReleaseFirstRead.Task.WaitAsync(cancellationToken);
                }

                if (ReadException != null)
                {
                    throw ReadException;
                }

                return Users;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReads);
            }
        }

        public Task<bool> SetUserActiveAsync(
            int externalUserId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        private void UpdateMaximum(int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _maximumConcurrentReads);
                if (current >= value)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(
                       ref _maximumConcurrentReads,
                       value,
                       current) != current);
        }
    }
}
