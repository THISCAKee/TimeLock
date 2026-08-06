using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeLockApp.Models;

public sealed class GoogleSheetUser
{
    public int UserId { get; init; }
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public int AllowedMinutes { get; init; }
    public string Role { get; init; } = "";
    public bool IsActive { get; init; }

    public static GoogleSheetUser Parse(
        IList<object> row,
        int rowNumber)
    {
        if (row.Count < 6)
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber} มีข้อมูลไม่ครบ 6 คอลัมน์");
        }

        string userIdText = GetCell(row, 0);
        string username = GetCell(row, 1);
        string password = GetCell(row, 2);
        string allowedMinutesText = GetCell(row, 3);
        string role = GetCell(row, 4).ToLowerInvariant();
        string isActiveText = GetCell(row, 5);


        if (!int.TryParse(
                userIdText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int userId) ||
            userId <= 0)
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: UserId ต้องเป็นเลขจำนวนเต็มมากกว่า 0");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: Username ห้ามว่าง");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: Password ห้ามว่าง");
        }

        if (!int.TryParse(
                allowedMinutesText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int allowedMinutes) ||
            allowedMinutes < 0)
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: AllowedMinutes ต้องเป็นเลขตั้งแต่ 0 ขึ้นไป");
        }

        if (role != "user" && role != "admin")
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: Role ต้องเป็น user หรือ admin");
        }

        if (!bool.TryParse(isActiveText, out bool isActive))
        {
            throw new InvalidOperationException(
                $"แถวที่ {rowNumber}: IsActive ต้องเป็น TRUE หรือ FALSE");
        }

        return new GoogleSheetUser
        {
            UserId = userId,
            Username = username.Trim(),
            Password = password,
            AllowedMinutes = allowedMinutes,
            Role = role,
            IsActive = isActive,
        };
    }

    private static string GetCell(
        IList<object> row,
        int index)
    {
        return Convert.ToString(
            row[index],
            CultureInfo.InvariantCulture)?.Trim() ?? "";
    }
}