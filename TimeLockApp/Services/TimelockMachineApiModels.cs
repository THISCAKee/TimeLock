using System.Text.Json.Serialization;

namespace TimeLockApp.Services;

internal interface ITimelockMachineApi
{
    Task<IReadOnlyList<TimelockMachineEvent>> GetEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task AcknowledgeEventAsync(
        string eventId,
        TimelockEventAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default);

    Task<TimelockExtensionCheck> CheckExtensionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<TimelockExtensionConfirmation> ConfirmExtensionAsync(
        string sessionId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);
}

internal sealed record TimelockMachineEvent(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("bookingId")] string BookingId,
    [property: JsonPropertyName("bookingNumber")] string BookingNumber,
    [property: JsonPropertyName("machineCode")] string MachineCode,
    [property: JsonPropertyName("startAt")] DateTimeOffset StartAt,
    [property: JsonPropertyName("endAt")] DateTimeOffset EndAt,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record TimelockEventAcknowledgement(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("processedAt")] DateTimeOffset ProcessedAt,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record TimelockExtensionCheck(
    [property: JsonPropertyName("canExtend")] bool CanExtend,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("currentEndAt")] DateTimeOffset CurrentEndAt,
    [property: JsonPropertyName("proposedEndAt")] DateTimeOffset? ProposedEndAt,
    [property: JsonPropertyName("extensionCount")] int ExtensionCount,
    [property: JsonPropertyName("maxExtensionCount")] int MaxExtensionCount);

internal sealed record TimelockExtensionConfirmation(
    [property: JsonPropertyName("bookingId")] string BookingId,
    [property: JsonPropertyName("endAt")] DateTimeOffset EndAt,
    [property: JsonPropertyName("extensionCount")] int ExtensionCount,
    [property: JsonPropertyName("allowedMinutes")] int AllowedMinutes);
