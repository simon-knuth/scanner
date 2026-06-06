# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Scanner ("Scanner for Windows") is an MSIX-packaged scanner app for Windows 11, published in the Microsoft Store. It is a **WinUI 3 / Windows App SDK desktop app on .NET 10**. It drives WIA-compatible scanners, edits the results (crop, rotate, ink, filters), and saves to PDF or image files.

## Keeping this file up-to-date

Treat this document as part of the codebase: when a change you make invalidates something here, update it in the **same change**. In particular, revise the relevant section when you:

- Add, remove, or rename a service — keep the DI registration notes (`App.xaml.cs`) and `Services/Interfaces/` guidance accurate.
- Introduce a new architectural pattern, message type convention, or `IProjectAction`, or change how the project/undo-redo/persistence layers fit together.
- Change the build, restore, deploy, or test workflow (commands, platforms, target framework, test runner).
- Add or swap a major dependency or integration (scanning, OCR, AI, PDF, telemetry, native interop) — update **Key integrations**.
- Change localization tooling, secrets handling, or the CI pipeline (`.github/workflows/build.yml`, `nuget.config`).
- Add or move a top-level project, window, or directory described under **Repository layout**.

Keep edits surgical and high-level: describe the "big picture" that requires reading several files to grasp. Do not turn this into an exhaustive file listing, and remove guidance that has become stale rather than letting it drift.

## Repository layout

The git root is one level above this file (`..`, contains `.git`, `README.md`, `.github/`). This directory (`Scanner/`) holds the solution; build/test commands run from here.

- `Scanner.slnx` — the solution (XML SLNX format, not `.sln`). References the main app, the test project, and the Tesseract submodule.
- `Scanner/` — the main app project (`Scanner.csproj`, root namespace `Scanner`).
- `ScannerTests/` — MSTest + FlaUI end-to-end UI automation project.
- `../Tesseract/` — git **submodule** (fork `simon-knuth/tesseract-arm64`) referenced directly as a project. Run `git submodule update --init --recursive` before first build.

## Build, run, and test

The app is platform-specific — there is **no AnyCPU**. You must pick a platform (`x86`, `x64`, or `ARM64`).

```powershell
# Restore (populates obj/ with RuntimeIdentifiers — required before first build)
msbuild Scanner.slnx /t:Restore /p:Configuration=Debug

# Build a specific platform
msbuild Scanner.slnx /p:Configuration=Debug /p:Platform=x64
```

Day-to-day, build/run/deploy from **Visual Studio** with the `Scanner` project set as startup and a platform selected — MSIX deployment registers the package locally, which the UI tests depend on.

### Tests

`ScannerTests` are **FlaUI UI-automation tests that launch the installed Store app** (`Application.LaunchStoreApp` with the AUMID in `ScannerTests/Constants.cs`). They are not in-process unit tests:

- The app MSIX **must be deployed locally first** (build/deploy `Scanner` from VS), or the launch fails.
- Tests drive the real UI by AutomationId. Element IDs are defined once in `Scanner/Tests/AutomationIds.cs` (namespace `Scanner.Tests`) and referenced from both the app XAML and the tests — add an ID there when you need to target a new control.
- Uses `EnableMSTestRunner` (Microsoft.Testing.Platform). Run via `dotnet test ScannerTests/ScannerTests.csproj` or the VS Test Explorer.
- Note `GeneralTests.cs` contains a hard-coded absolute path to test images (`D:\GitHub\scanner\...`) — environment-specific.

To exercise scanning **without physical hardware**, the UI exposes an "Add debug scanner" action (`Models/ScanningDevices/DebugScanner.cs`) that lets you scan from local image files.

## Architecture

Strict **MVVM** with `CommunityToolkit.Mvvm`. Three layers under `Scanner/`: `Views/` (XAML + code-behind), `ViewModels/`, `Models/`. ViewModels and models derive from `ObservableObject`/`ObservableRecipient`; commands are `[RelayCommand]`.

**Dependency injection** — all services are registered as singletons in `App.xaml.cs` via `Ioc.Default.ConfigureServices(...)`. Resolve with `Ioc.Default.GetRequiredService<IFoo>()`. Each service in `Services/` has an interface in `Services/Interfaces/` and is referenced through that interface. When adding a service, register it in `App.xaml.cs` and add the interface.

**Messaging** — components communicate through `WeakReferenceMessenger.Default` (CommunityToolkit) rather than direct references. Message types live in `Messages/` (e.g. `ShowSaveOptionsDialogMessage`, `ApplyTemplateMessage`). Views/dialogs typically subscribe to a `Show…Message` to present themselves. Prefer a message over coupling a ViewModel to a View.

**Project model** (`Models/Project/`) — the central editing concept. `ProjectBase` (abstract `ObservableRecipient`) is specialized by `PdfProject` and `MultiFileProject`. `IProjectService` owns the single `CurrentProject`, page selection, scan/edit state machine (`ScanState`), and persistence. Note: `ProjectBase` pulls its dependencies via `Ioc.Default` static fields rather than constructor injection.

**Undo/redo** — a command pattern. Every editing operation is an `IProjectAction` in `Models/ProjectActions/` (e.g. `CropPagesAction`, `RotatePagesAction`, `RenameAction`). Apply through `IProjectService.ApplyActionAsync`; the service maintains `UndoStack`/`RedoStack`. Add a new `IProjectAction` to support a new editing operation rather than mutating pages directly.

**Persistence** — EF Core with SQLite, split into three `DbContext`s in `Data/`: `KnownScannersDbContext`, `ProjectHistoryDbContext`, `TemplatesDbContext`, each fronted by a service (`KnownScannersService`, `ProjectHistoryService`, `TemplatesService`).

**Windows & app lifecycle** — three top-level windows in `AppWindows/`: `MainWindow`, `SettingsWindow`, `FeedbackWindow` (latter two created on demand via `App.ShowSettings`/`ShowFeedback`). The app is **single-instance**: `Program.cs` defines a custom `Main` (`DISABLE_XAML_GENERATED_MAIN` is set in the csproj) that uses `AppInstance` key registration and redirects activation to the existing instance.

### Key integrations

- **Scanning**: `Windows.Devices.Scanners` (WIA) via `ScannerDiscoveryService`; hardware in `Models/ScanningDevices/HardwareScanner.cs`.
- **OCR**: Tesseract (the submodule). `OcrService` + training data under `Resources/Tesseract Training Data/`.
- **AI features**: `CopilotRuntimeService` uses the Windows Copilot Runtime (on-device models, e.g. Phi Silica) — gated behind Copilot+ hardware availability.
- **PDF**: PDFsharp.
- **Native interop**: `Microsoft.Windows.CsWin32` (source-generated P/Invoke; see `NativeMethods.txt` if present) and CsWinRT.
- **Telemetry / logging**: Sentry (`SentryService`) and Serilog (`LogService`, async file sink). Crashes/unobserved exceptions are funneled through `App.xaml.cs` handlers with a plain-text fallback log.

### Localization

UI strings are localized via **ReswPlus** (`Resources/Strings/`), with ~20 languages. ReswPlus generates strongly-typed accessors from the `.resw` files — edit the resource files, not generated code.

## Conventions

- `Nullable` is enabled across both projects; honor nullable annotations.
- Source files use distinctive banner comment blocks (`// DECLARATIONS //…`, `// METHODS //…`) to section classes — match this when editing existing files.
- WinUI XAML lives beside its code-behind (`*.xaml` + `*.xaml.cs`); `EnableXamlSourceGeneration` is on.

## Secrets & CI

- `Resources/Secrets.resx` ships with a literal placeholder `SENTRY_DSN_GOES_HERE`. The GitHub Actions release build (`.github/workflows/build.yml`) replaces it with the real DSN from secrets — **do not commit a real DSN** into this file.
- CI builds the MSIX for all three platforms on push to `main`, signs it with a PFX from secrets, and creates a Sentry release. Local builds do not need the certificate for `Debug`.
- An extra NuGet feed (CommunityToolkit Labs) is configured in `nuget.config` for the `SegmentedControl` preview package.
