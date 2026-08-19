using TimeLockApp.Services;

internal static class TimelockGatewayTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "device configuration normalizes machine code and backend URL",
            DeviceConfigurationNormalizesValues);
        yield return (
            "offline verifier accepts only the matching password",
            OfflineVerifierChecksPassword);
        yield return (
            "heartbeat reports logged out as online and logged in as active",
            HeartbeatPayloadTracksSession);
    }

    private static void DeviceConfigurationNormalizesValues()
    {
        TimelockDeviceConfiguration configuration =
            TimelockDeviceConfiguration.Create(
                " pc-001 ",
                " token ",
                "https://booking-ai-lab.vercel.app/");

        AssertTrue(configuration.MachineCode == "PC-001", "Machine code must be normalized.");
        AssertTrue(configuration.DeviceToken == "token", "Device token must be trimmed.");
        AssertTrue(configuration.BackendUrl == "https://booking-ai-lab.vercel.app", "Backend URL must omit its trailing slash.");
    }

    private static void OfflineVerifierChecksPassword()
    {
        PasswordVerifier verifier = PasswordVerifier.Create("correct", iterations: 1_000);
        AssertTrue(verifier.Verify("correct"), "Original password must pass.");
        AssertTrue(!verifier.Verify("wrong"), "Different password must fail.");
    }

    private static void HeartbeatPayloadTracksSession()
    {
        TimelockHeartbeat online = TimelockHeartbeat.Online("PC-001", "1.0.0", "Windows 11");
        TimelockHeartbeat active = TimelockHeartbeat.Active("PC-001", "student01", "1.0.0", "Windows 11");

        AssertTrue(online.SessionStatus == "logged_out" && online.Username is null, "Online heartbeat must have no user.");
        AssertTrue(active.SessionStatus == "logged_in" && active.Username == "student01", "Active heartbeat must identify the user.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
