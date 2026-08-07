using TimeLockApp.Models;

namespace TimeLockApp.Services;

public interface IGoogleSheetsUserService
{
    Task<IReadOnlyList<GoogleSheetUser>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<bool> SetUserActiveAsync(
        int externalUserId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
