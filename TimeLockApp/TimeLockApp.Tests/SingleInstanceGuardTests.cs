using TimeLockApp.Services;

internal static class SingleInstanceGuardTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "single instance guard rejects a duplicate and releases ownership",
            RejectsDuplicateAndReleasesOwnership);
    }

    private static void RejectsDuplicateAndReleasesOwnership()
    {
        string mutexName = $@"Local\TimeLockApp.Tests.{Guid.NewGuid():N}";

        using (SingleInstanceGuard first = SingleInstanceGuard.TryAcquire(mutexName))
        {
            Assert(first.IsOwner, "The first process must own the guard.");

            using SingleInstanceGuard duplicate = SingleInstanceGuard.TryAcquire(mutexName);
            Assert(!duplicate.IsOwner, "A duplicate process must be rejected.");
        }

        using SingleInstanceGuard replacement = SingleInstanceGuard.TryAcquire(mutexName);
        Assert(replacement.IsOwner, "Ownership must be available after disposal.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
