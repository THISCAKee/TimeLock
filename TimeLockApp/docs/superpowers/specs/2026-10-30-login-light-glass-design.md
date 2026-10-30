# Login Light Glass Design

## Intent and goals

Restyle the existing full-screen TimeLockApp login window with a calm light-glass look that feels polished while keeping the username and password form easy to read. Show a concise in-app usage guide beside the login form as soon as the screen opens. Preserve the existing bilingual copy, logo, network connection and shutdown actions, and authentication behavior.

## Approaches

- **Light glass (chosen):** cool, pale background with restrained soft color fields and one translucent white login surface. It keeps the kiosk screen legible and focused.
- **Dark glass:** navy background and a brighter translucent surface. It is more dramatic but less suited to a long-running login screen.
- **Split layout:** separate brand and login areas. It adds structure but changes the current compact, centered composition and gives the login less focus.

## Constraints and implementation decisions

- Keep the full-screen, topmost WPF window and the existing centered form composition.
- Keep the current logo, Thai/English controls, username and password fields, login action, network status and authentication action, shutdown action, dynamic resources, and event handlers.
- Limit changes to presentation in `MainWindow.xaml` and shared presentation resources only if needed; do not change authentication or window behavior.
- Build the glass treatment with native WPF brushes, borders, and shadows. Prioritize contrast and focus visibility over transparency effects.
- Make disabled login and network actions visibly muted while preserving their existing enabled/disabled behavior.
- Show the login form and usage guide side by side in the same full-screen window. The guide is visible by default and requires no action to open.
- Summarize the existing usage guide: account prerequisites, login steps, internet authentication, remaining-time/logout behavior, and basic troubleshooting.

## Non-goals

- No authentication behavior changes, booking workflow, external manual artwork, or network-dependent guide content.
- No system-wide acrylic/backdrop dependency or new package.
