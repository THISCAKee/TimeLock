using System.Windows;

namespace TimeLockApp.Services;

public sealed class LanguageService
{
    public static LanguageService Default { get; } = new();

    private static readonly IReadOnlyDictionary<string, string> Thai =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LoginButton"] = "เข้าสู่ระบบ",
            ["SyncFailed"] = "ซิงก์ไม่สำเร็จ: {0}",
            ["AppTitle"] = "Time Lock App",
            ["ShutdownComputer"] = "ปิดเครื่อง",
            ["ShutdownConfirm"] = "ต้องการปิดเครื่องคอมพิวเตอร์ใช่หรือไม่?",
            ["ShutdownConfirmTitle"] = "ยืนยันการปิดเครื่อง",
            ["ShutdownFailed"] = "ไม่สามารถสั่งปิดเครื่องได้: {0}",
            ["LoginHeading"] = "กรุณาเข้าสู่ระบบเพื่อเริ่มใช้งาน",
            ["LoginHelp"] = "ติดต่อเจ้าหน้าที่หากมีปัญหาในการเข้าสู่ระบบ",
            ["Username"] = "Username",
            ["UsernamePlaceholder"] = "กรอกชื่อผู้ใช้งาน",
            ["Password"] = "Password",
            ["PasswordPlaceholder"] = "กรอกรหัสผ่าน",
            ["NetworkHint"] = "หากยังไม่ได้ Authen Internet กรุณากดเชื่อมต่ออินเทอร์เน็ต",
            ["ConnectInternet"] = "เชื่อมต่ออินเทอร์เน็ต",
            ["AdminPanel"] = "Admin Panel",
            ["UninstallProgram"] = "ถอนการติดตั้งโปรแกรม",
            ["UninstallConfirm"] = "ต้องการถอนการติดตั้ง TimeLockApp ใช่หรือไม่?",
            ["UninstallConfirmTitle"] = "ยืนยันการถอนการติดตั้ง",
            ["UninstallFailed"] = "ไม่สามารถเริ่มตัวถอนการติดตั้งได้: {0}",
            ["AdminSubtitle"] = "จัดการผู้ใช้งานและกำหนดเวลาการใช้งาน",
            ["Users"] = "Users",
            ["SelectUserHint"] = "เลือกผู้ใช้เพื่อแก้ไขข้อมูล",
            ["Show"] = "แสดง",
            ["Hide"] = "ซ่อน",
            ["Minutes"] = "Minutes",
            ["Role"] = "Role",
            ["UserDetail"] = "User Detail",
            ["UserDetailSubtitle"] = "เพิ่มหรือแก้ไขข้อมูลผู้ใช้งาน",
            ["AllowedMinutes"] = "Allowed Minutes",
            ["UserRole"] = "user",
            ["AdminRole"] = "admin",
            ["Add"] = "เพิ่ม",
            ["Edit"] = "แก้ไข",
            ["DeleteUser"] = "ลบ User",
            ["ClearForm"] = "ล้างฟอร์ม",
            ["AdminTip"] = "Tip: เลือก user จากตารางด้านซ้ายเพื่อแก้ไขข้อมูล",
            ["History"] = "ประวัติการใช้งาน",
            ["SyncUsers"] = "Sync Users",
            ["BackToLogin"] = "กลับหน้า Login",
            ["ExitProgram"] = "ปิดโปรแกรม",
            ["NetworkAuthTitle"] = "เชื่อมต่ออินเทอร์เน็ต",
            ["NetworkAuthLoading"] = "กำลังเปิดหน้าระบบยืนยันตัวตน...",
            ["Cancel"] = "ยกเลิก",
            ["NetworkLoading"] = "กำลังโหลดหน้าระบบ Authen...",
            ["NetworkErrorTitle"] = "ไม่สามารถเปิดหน้าระบบ Authen ได้",
            ["NetworkErrorHint"] = "กรุณาตรวจสอบการเชื่อมต่อเครือข่าย",
            ["Retry"] = "ลองใหม่",
            ["NetworkAuthFooter"] = "เมื่อยืนยันตัวตนสำเร็จ ระบบจะกลับไปหน้า Login อัตโนมัติ",
            ["AlertTitle"] = "แจ้งเตือน",
            ["AlertMessage"] = "ข้อความแจ้งเตือน",
            ["Ok"] = "OK",
            ["SessionHistory"] = "Session History",
            ["SessionHistorySubtitle"] = "ประวัติการใช้งานของผู้ใช้ทั้งหมด",
            ["Refresh"] = "Refresh",
            ["ClearHistory"] = "ล้างประวัติ",
            ["Id"] = "ID",
            ["StartTime"] = "Start Time",
            ["EndTime"] = "End Time",
            ["Allowed"] = "Allowed",
            ["Used"] = "Used",
            ["Status"] = "Status",
            ["Close"] = "ปิด",
            ["RemainingTime"] = "เวลาคงเหลือ",
            ["Logout"] = "ออก",
            ["Language"] = "ภาษา",
            ["LoggingIn"] = "กำลังเข้าสู่ระบบ...",
            ["InvalidCredentials"] = "Username หรือ Password ไม่ถูกต้อง",
            ["OpeningAuth"] = "กำลังเปิดระบบ Authen Internet...",
            ["InternetConnectedSyncing"] = "เชื่อมต่ออินเทอร์เน็ตสำเร็จ กำลังซิงค์ข้อมูล...",
            ["AuthCancelled"] = "ยกเลิก Authen กรุณาเชื่อมต่ออีกครั้งเมื่อต้องการใช้อินเทอร์เน็ต",
            ["OpenAuthFailed"] = "ไม่สามารถเปิดระบบ Authen ได้: {0}",
            ["CheckingInternet"] = "กำลังตรวจสอบการเชื่อมต่ออินเทอร์เน็ต...",
            ["NoInternetAuth"] = "ยังไม่ได้ Authen Internet",
            ["KeyboardLockFailed"] = "ไม่สามารถเปิดระบบล็อกแป้นพิมพ์ได้ (Win32: {0})",
            ["KeyboardLockFailedTitle"] = "เริ่มระบบล็อกไม่สำเร็จ",
            ["WebsiteOpenFailedTitle"] = "ไม่สามารถเปิดเว็บไซต์ได้",
            ["TimeExpiredTitle"] = "หมดเวลา",
            ["TimeExpiredMessage"] = "หมดเวลาใช้งานแล้ว กรุณากด OK เพื่อกลับสู่หน้า Login",
            ["LogoutConfirm"] = "ต้องการออกจากระบบหรือไม่?",
            ["LogoutConfirmTitle"] = "ยืนยันการออกจากระบบ",
            ["ClearHistoryConfirm"] = "ต้องการล้างประวัติการใช้งานทั้งหมดใช่หรือไม่?\n\nการกระทำนี้ไม่สามารถย้อนกลับได้",
            ["ClearHistoryConfirmTitle"] = "ยืนยันการล้างประวัติ",
            ["HistoryCleared"] = "ล้างประวัติการใช้งานเรียบร้อยแล้ว",
            ["Success"] = "สำเร็จ",
            ["ConnectingAuth"] = "กำลังเชื่อมต่อระบบยืนยันตัวตน...",
            ["LoadWebFailed"] = "โหลดหน้าเว็บไม่สำเร็จ: {0}",
            ["NetworkLoginPrompt"] = "กรุณาเข้าสู่ระบบเครือข่ายมหาวิทยาลัย",
            ["AuthSuccessChecking"] = "ยืนยันตัวตนสำเร็จ กำลังตรวจสอบการเชื่อมต่ออินเทอร์เน็ต...",
            ["AuthSuccessNoInternet"] = "ยืนยันตัวตนแล้ว แต่ยังไม่สามารถเชื่อมต่ออินเทอร์เน็ตได้",
            ["InternetCheckFailed"] = "ตรวจสอบอินเทอร์เน็ตไม่สำเร็จ: {0}",
            ["NetworkConnectionFailed"] = "เชื่อมต่อไม่สำเร็จ",
            ["AddFailed"] = "เพิ่ม user ไม่สำเร็จ อาจมี username นี้อยู่แล้ว",
            ["AddSuccess"] = "เพิ่ม user สำเร็จ",
            ["SelectEditUser"] = "กรุณาเลือก user ที่ต้องการแก้ไข",
            ["EditFailed"] = "แก้ไข user ไม่สำเร็จ",
            ["EditSuccess"] = "แก้ไข user สำเร็จ",
            ["SelectDeleteUser"] = "กรุณาเลือก user ที่ต้องการลบ",
            ["DeleteAdminWarning"] = "ไม่แนะนำให้ลบ admin ผ่านหน้านี้",
            ["DeleteConfirm"] = "ต้องการลบ user '{0}' ใช่หรือไม่?",
            ["DeleteConfirmTitle"] = "ยืนยันการลบ",
            ["DeleteFailed"] = "ลบ user ไม่สำเร็จ",
            ["DeleteSuccess"] = "ลบ user สำเร็จ",
            ["EnterUsername"] = "กรุณากรอก username",
            ["EnterPassword"] = "กรุณากรอก password",
            ["MinutesNumber"] = "Allowed Minutes ต้องเป็นตัวเลข",
            ["MinutesNonNegative"] = "Allowed Minutes ต้องไม่ติดลบ",
            ["MinutesPositive"] = "user ปกติต้องมีเวลามากกว่า 0 นาที",
            ["SyncingUsers"] = "กำลังซิงก์ข้อมูลผู้ใช้...",
            ["SyncSuccess"] = "ซิงก์สำเร็จ {0} รายการ",
            ["Warning30Minutes"] = "เหลือเวลาใช้งานอีก 30 นาที",
            ["Warning10Minutes"] = "เหลือเวลาใช้งานอีก 10 นาที",
            ["Warning1Minute"] = "เหลือเวลาใช้งานอีก 1 นาที",
            ["LatestSync"] = "ซิงก์ล่าสุด {0:HH:mm:ss}: {1} รายการ",
            ["LatestSyncFailed"] = "ซิงก์ไม่สำเร็จ: {0} (จะลองใหม่อัตโนมัติ)",
            ["InvalidWebsiteUrl"] = "URL เว็บไซต์ไม่ถูกต้อง",
            ["ChromeNotFound"] = "ไม่พบ Google Chrome ในเครื่องนี้",
            ["ChromeOpenFailed"] = "ไม่สามารถเปิด Google Chrome ได้",
            ["ServiceAccountNotFound"] = "ไม่พบไฟล์ Service Account",
            ["SessionNotFound"] = "ไม่พบ session ที่ต้องการสิ้นสุด",
            ["DuplicateSheetUsername"] = "พบ Username ซ้ำใน Google Sheet: {0}",
            ["AdminInSheet"] = "ไม่ต้องเพิ่ม admin ลง Google Sheet เพราะ admin ถูกเก็บในเครื่องอยู่แล้ว",
            ["SheetRowIncomplete"] = "แถวที่ {0} มีข้อมูลไม่ครบ 6 คอลัมน์",
            ["SheetUserIdInvalid"] = "แถวที่ {0}: UserId ต้องเป็นเลขจำนวนเต็มมากกว่า 0",
            ["SheetUsernameEmpty"] = "แถวที่ {0}: Username ห้ามว่าง",
            ["SheetPasswordEmpty"] = "แถวที่ {0}: Password ห้ามว่าง",
            ["SheetMinutesInvalid"] = "แถวที่ {0}: AllowedMinutes ต้องเป็นเลขตั้งแต่ 0 ขึ้นไป",
            ["SheetRoleInvalid"] = "แถวที่ {0}: Role ต้องเป็น user หรือ admin",
            ["SheetActiveInvalid"] = "แถวที่ {0}: IsActive ต้องเป็น TRUE หรือ FALSE",
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(Thai.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal), StringComparer.Ordinal)
        {
            ["LoginButton"] = "Log in",
            ["ShutdownComputer"] = "Shut down computer",
            ["ShutdownConfirm"] = "Shut down this computer now?",
            ["ShutdownConfirmTitle"] = "Confirm shutdown",
            ["ShutdownFailed"] = "Unable to shut down the computer: {0}",
            ["SyncFailed"] = "Sync failed: {0}",
            ["LoginHeading"] = "Log in to start",
            ["LoginHelp"] = "Contact an administrator if you have trouble logging in",
            ["UsernamePlaceholder"] = "Enter username",
            ["PasswordPlaceholder"] = "Enter password",
            ["NetworkHint"] = "If Internet authentication is required, click connect",
            ["ConnectInternet"] = "Connect to Internet",
            ["AdminSubtitle"] = "Manage users and usage time limits",
            ["SelectUserHint"] = "Select a user to edit details",
            ["Show"] = "Show",
            ["Hide"] = "Hide",
            ["UserDetailSubtitle"] = "Add or edit user details",
            ["Add"] = "Add",
            ["Edit"] = "Edit",
            ["DeleteUser"] = "Delete User",
            ["ClearForm"] = "Clear form",
            ["AdminTip"] = "Tip: Select a user from the table to edit details",
            ["History"] = "Usage history",
            ["BackToLogin"] = "Back to Login",
            ["ExitProgram"] = "Exit",
            ["UninstallProgram"] = "Uninstall Program",
            ["UninstallConfirm"] = "Uninstall TimeLockApp from this computer?",
            ["UninstallConfirmTitle"] = "Confirm uninstall",
            ["UninstallFailed"] = "Unable to start the uninstaller: {0}",
            ["NetworkAuthTitle"] = "Connect to Internet",
            ["NetworkAuthLoading"] = "Opening authentication page...",
            ["Cancel"] = "Cancel",
            ["NetworkLoading"] = "Loading authentication page...",
            ["NetworkErrorTitle"] = "Unable to open authentication page",
            ["NetworkErrorHint"] = "Please check the network connection",
            ["Retry"] = "Retry",
            ["NetworkAuthFooter"] = "After authentication succeeds, the app will return to Login automatically",
            ["AlertTitle"] = "Alert",
            ["AlertMessage"] = "Alert message",
            ["SessionHistorySubtitle"] = "All user usage history",
            ["ClearHistory"] = "Clear history",
            ["Close"] = "Close",
            ["RemainingTime"] = "Time remaining",
            ["Logout"] = "Log out",
            ["Language"] = "Language",
            ["LoggingIn"] = "Logging in...",
            ["InvalidCredentials"] = "Invalid username or password",
            ["OpeningAuth"] = "Opening Internet authentication...",
            ["InternetConnectedSyncing"] = "Internet connected. Syncing data...",
            ["AuthCancelled"] = "Authentication cancelled. Connect again when Internet access is needed",
            ["OpenAuthFailed"] = "Unable to open authentication: {0}",
            ["CheckingInternet"] = "Checking Internet connection...",
            ["NoInternetAuth"] = "Internet authentication is required",
            ["KeyboardLockFailed"] = "Unable to enable keyboard lock (Win32: {0})",
            ["KeyboardLockFailedTitle"] = "Keyboard lock startup failed",
            ["WebsiteOpenFailedTitle"] = "Unable to open website",
            ["TimeExpiredTitle"] = "Time expired",
            ["TimeExpiredMessage"] = "Your usage time has expired. Press OK to return to Login",
            ["LogoutConfirm"] = "Do you want to log out?",
            ["LogoutConfirmTitle"] = "Confirm logout",
            ["ClearHistoryConfirm"] = "Clear all usage history?\n\nThis action cannot be undone",
            ["ClearHistoryConfirmTitle"] = "Confirm clear history",
            ["HistoryCleared"] = "Usage history cleared",
            ["Success"] = "Success",
            ["ConnectingAuth"] = "Connecting to authentication...",
            ["LoadWebFailed"] = "Failed to load web page: {0}",
            ["NetworkLoginPrompt"] = "Please log in to the university network",
            ["AuthSuccessChecking"] = "Authentication succeeded. Checking Internet connection...",
            ["AuthSuccessNoInternet"] = "Authenticated, but Internet access is still unavailable",
            ["InternetCheckFailed"] = "Internet check failed: {0}",
            ["NetworkConnectionFailed"] = "Connection failed",
            ["AddFailed"] = "Could not add user. The username may already exist",
            ["AddSuccess"] = "User added successfully",
            ["SelectEditUser"] = "Select a user to edit",
            ["EditFailed"] = "Could not edit user",
            ["EditSuccess"] = "User edited successfully",
            ["SelectDeleteUser"] = "Select a user to delete",
            ["DeleteAdminWarning"] = "Deleting an admin from this page is not recommended",
            ["DeleteConfirm"] = "Delete user '{0}'?",
            ["DeleteConfirmTitle"] = "Confirm delete",
            ["DeleteFailed"] = "Could not delete user",
            ["DeleteSuccess"] = "User deleted successfully",
            ["EnterUsername"] = "Enter username",
            ["EnterPassword"] = "Enter password",
            ["MinutesNumber"] = "Allowed Minutes must be a number",
            ["MinutesNonNegative"] = "Allowed Minutes cannot be negative",
            ["MinutesPositive"] = "A regular user must have more than 0 minutes",
            ["SyncingUsers"] = "Syncing users...",
            ["SyncSuccess"] = "Synced {0} users",
            ["Warning30Minutes"] = "30 minutes of usage time remaining",
            ["Warning10Minutes"] = "10 minutes of usage time remaining",
            ["Warning1Minute"] = "1 minute of usage time remaining",
            ["LatestSync"] = "Last sync {0:HH:mm:ss}: {1} users",
            ["LatestSyncFailed"] = "Sync failed: {0} (will retry automatically)",
            ["InvalidWebsiteUrl"] = "Invalid website URL",
            ["ChromeNotFound"] = "Google Chrome was not found on this computer",
            ["ChromeOpenFailed"] = "Unable to open Google Chrome",
            ["ServiceAccountNotFound"] = "Service Account file not found",
            ["SessionNotFound"] = "The session to end was not found",
            ["DuplicateSheetUsername"] = "Duplicate username in Google Sheet: {0}",
            ["AdminInSheet"] = "Do not add admin to Google Sheet because admin is stored locally",
            ["SheetRowIncomplete"] = "Row {0} does not contain all 6 columns",
            ["SheetUserIdInvalid"] = "Row {0}: UserId must be a positive integer",
            ["SheetUsernameEmpty"] = "Row {0}: Username cannot be empty",
            ["SheetPasswordEmpty"] = "Row {0}: Password cannot be empty",
            ["SheetMinutesInvalid"] = "Row {0}: AllowedMinutes must be a number of 0 or greater",
            ["SheetRoleInvalid"] = "Row {0}: Role must be user or admin",
            ["SheetActiveInvalid"] = "Row {0}: IsActive must be TRUE or FALSE",
        };

    public string CurrentLanguage { get; private set; } = "th";

    public event EventHandler? LanguageChanged;

    public string Get(string key, params object[] args)
    {
        IReadOnlyDictionary<string, string> values =
            CurrentLanguage == "en" ? English : Thai;

        if (!values.TryGetValue(key, out string? value))
        {
            return key;
        }

        return args.Length == 0 ? value : string.Format(value, args);
    }

    public void SetLanguage(string language)
    {
        string normalized =
            string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : "th";

        bool languageUnchanged = normalized == CurrentLanguage;
        CurrentLanguage = normalized;

        if (Application.Current != null)
        {
            IReadOnlyDictionary<string, string> values =
                CurrentLanguage == "en" ? English : Thai;
            ResourceDictionary dictionary = new();

            foreach ((string key, string value) in values)
            {
                dictionary[key] = value;
            }

            ResourceDictionary? current = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(item => item.Contains("LoginButton"));

            if (current != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(current);
            }

            Application.Current.Resources.MergedDictionaries.Insert(0, dictionary);
        }

        if (!languageUnchanged)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
