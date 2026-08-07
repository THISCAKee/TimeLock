using TimeLockApp.Data;
using TimeLockApp.Models;

internal static class UserSynchronizationChangeTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("sheet insert reports a material change", InsertReportsChange);
        yield return ("identical sheet data reports no change", IdenticalDataReportsNoChange);
        yield return ("sheet update reports a material change", UpdateReportsChange);
        yield return ("sheet deletion reports a change and keeps local users", DeletionReportsChangeAndKeepsLocalUsers);
    }

    private static void InsertReportsChange()
    {
        using var fixture = new TestDatabase();

        bool inserted = fixture.Service.SynchronizeUsers(
            new[] { SheetUser(501, "new-user") });

        AssertTrue(inserted, "A Sheet insert must report a material change.");
    }

    private static void IdenticalDataReportsNoChange()
    {
        using var fixture = new TestDatabase();
        GoogleSheetUser user = SheetUser(502, "same-user");
        fixture.Service.SynchronizeUsers(new[] { user });

        bool identical = fixture.Service.SynchronizeUsers(new[] { user });

        AssertFalse(identical, "Identical Sheet data must not report a change.");
    }

    private static void UpdateReportsChange()
    {
        using var fixture = new TestDatabase();
        fixture.Service.SynchronizeUsers(
            new[] { SheetUser(503, "updated-user") });

        bool changed = fixture.Service.SynchronizeUsers(
            new[] { SheetUser(503, "updated-user", password: "changed") });

        AssertTrue(changed, "A changed Sheet field must report a material change.");
    }

    private static void DeletionReportsChangeAndKeepsLocalUsers()
    {
        using var fixture = new TestDatabase();
        fixture.Service.SynchronizeUsers(
            new[] { SheetUser(504, "deleted-user") });

        bool deleted = fixture.Service.SynchronizeUsers(
            Array.Empty<GoogleSheetUser>());

        AssertTrue(deleted, "A missing Sheet user must report a deletion.");
        AssertTrue(
            fixture.Service.GetAllUsers().Any(user => user.IsLocalOnly),
            "Local-only users must remain.");
    }

    private static GoogleSheetUser SheetUser(
        int id,
        string username,
        string password = "password")
    {
        return new GoogleSheetUser
        {
            UserId = id,
            Username = username,
            Password = password,
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

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }
}
