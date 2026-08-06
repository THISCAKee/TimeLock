# Open Chrome After User Login Design

## Objective

Open Google Chrome automatically at `https://libmsu-ai.vercel.app/` after an ordinary user logs in successfully and the timed session has started.

## Scope

- Apply only to timed ordinary users; administrator login continues to open the Admin Panel without launching Chrome.
- Start the local session, timer, and usage window before attempting to launch Chrome.
- Launch once per successful user login/session.
- If Chrome is already running, allow Chrome to open the URL as a new tab using its normal command-line behavior.
- Do not fall back to another browser.

## Architecture

Add a focused `ChromeLauncherService` that owns executable discovery and process startup. `MainWindow` calls the service at the end of `StartSession`; it does not contain Registry or filesystem lookup details.

The launcher searches for `chrome.exe` in this order:

1. Windows App Paths in current-user and local-machine Registry hives, across available 64-bit and 32-bit views.
2. `%ProgramFiles%\Google\Chrome\Application\chrome.exe`.
3. `%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe`.
4. `%LocalAppData%\Google\Chrome\Application\chrome.exe`.

The service starts the discovered executable with the approved URL as a quoted argument and returns a result containing success or a user-safe error message. All lookup and process-start exceptions are caught inside the service.

## User Experience

After successful user authentication:

1. The session database row is created.
2. The login window hides.
3. The usage timer appears and begins counting.
4. Chrome opens `https://libmsu-ai.vercel.app/`.

If Chrome cannot be found or started, show a warning owned by the usage window. Dismissing the warning leaves the session and timer running. The warning must not expose a stack trace or internal filesystem details.

## Error Handling

- Missing Chrome returns “ไม่พบ Google Chrome ในเครื่องนี้”.
- Process startup failure returns “ไม่สามารถเปิด Google Chrome ได้”.
- Browser failure never rolls back the session, restores login access, or stops the timer.
- A repeated login creates a new session and therefore makes one new launch attempt.

## Verification Contract

Codex will not launch Chrome, run the application, build, or test. The user will verify:

1. Ordinary user login opens the approved URL in Chrome once.
2. The usage timer is already visible and running when Chrome opens.
3. Administrator login does not launch Chrome.
4. Existing Chrome opens the URL in a new tab.
5. Missing/broken Chrome shows a warning while the timer continues.
