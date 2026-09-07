using TimeLockApp.Services;

internal static class WebsiteEndpointsTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "user website points to the LibMSU deployment",
            UserWebsitePointsToLibMsu);
    }

    private static void UserWebsitePointsToLibMsu()
    {
        AssertTrue(
            WebsiteEndpoints.UserWebsiteUrl == "https://libmsu-ai.vercel.app/",
            "TimeLockApp must open the LibMSU website after login.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
