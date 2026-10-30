# Login Light Glass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the full-screen TimeLockApp login window with a readable light-glass appearance and show a concise bilingual usage guide alongside the login form.

**Architecture:** Keep the existing WPF login behavior and bindings. Apply the glass treatment with native WPF gradients, translucent fills, borders, and shadows. Place a localized guide panel beside the login form in the same maximized window.

**Tech Stack:** WPF, XAML, .NET

**Spec:** `docs/superpowers/specs/2026-10-30-login-light-glass-design.md`

## Global Constraints

- Keep the full-screen, topmost WPF window and centered login composition.
- Preserve all current controls, dynamic resources, names, and event handlers.
- Use native WPF presentation primitives; add no packages or system backdrop dependency.
- Prioritize contrast and focus visibility over transparency effects.
- Keep authentication behavior unchanged; the usage guide is informational and visible by default.
- Show Thai or English guide content using the current `LanguageService` selection.

## Review Focus

- Thai and English labels still resolve through their existing dynamic resources.
- Username and password placeholders remain aligned and legible.
- Login, network authentication, shutdown, and language controls retain their existing names and handlers.
- Error, network status, and disabled button states remain distinguishable on the new surfaces.
- The window remains visually centered at maximized kiosk size.

---

### Task 1: Apply the light-glass login appearance

**Files:**
- Modify: `MainWindow.xaml`

**Interfaces:**
- Consumes: Existing resources from `App.xaml` and existing event handlers in `MainWindow.xaml.cs`.
- Produces: A restyled login view with the same XAML names, dynamic resources, control order, and interactions.

- [x] Update the login-window background and central card in `MainWindow.xaml` to use a pale cool backdrop, restrained soft color fields, a translucent light surface, a fine bright edge, and a soft shadow.
- [x] Refine spacing, input surfaces, and button finishes only as needed to maintain clear hierarchy and readable focus states; keep all current controls and bindings.
- [x] Add a muted opacity state to the login and network buttons when disabled, without changing their existing enabled/disabled behavior.
- [x] Run a temporary XAML contract check before and after the restyle; it failed before the gradient surfaces existed and passed after the change.
- [x] Build a temporary source copy with `dotnet build <temp>\TimeLockApp.csproj --no-restore` to avoid overwriting pre-existing tracked `bin/obj` changes; expected result: `Build succeeded`.
- [x] Review the XAML diff to confirm it changes presentation only and preserves all existing `x:Name`, `DynamicResource`, and event-handler references.

### Task 2: Add a bilingual usage-guide page to login

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `Services/LanguageService.cs`
- Modify: `TimeLockApp.Tests/LanguageServiceTests.cs`

**Interfaces:**
- Consumes: Existing `LanguageService` selected language and the usage steps in `docs/booking-user-guide-th.md`.
- Produces: A side-by-side guide panel visible by default and Thai/English guide resource strings.

- [x] Add a language-service test that requires all usage-guide resource keys in Thai and English; run it before implementation and confirm it fails on the missing guide label.
- [x] Add concise Thai and English resource strings for prerequisites, login, internet authentication, session timing/logout, troubleshooting, and the guide footer.
- [x] Add the glass-styled usage-guide panel alongside the login form in `MainWindow.xaml`; keep both visible by default.
- [x] Verify the guide has no launch button or collapsed visibility state and shares a two-column layout with the login form.
- [x] Run the existing `TimeLockApp.Tests` console suite from a temporary project copy so pre-existing tracked `bin/obj` changes remain untouched.
- [x] Review the XAML/C# diff to confirm the guide is informational and does not alter login/authentication behavior.
