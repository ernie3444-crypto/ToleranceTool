# Tolerance Tool

An Excel add-in that standardizes tolerance calculations on engineering test
datasheets. Built on **Excel-DNA**, **C# / .NET Framework 4.8**, **64-bit Excel**.

See [`docs/ToleranceTool-Architecture.html`](docs/ToleranceTool-Architecture.html)
for the full design and the phased build plan, and
[`docs/UserGuide.md`](docs/UserGuide.md) for how to use the finished add-in.

## Solution layout

| Project | Role |
| --- | --- |
| `src/ToleranceTool.Core` | Domain model + calculation engine. No Excel, no I/O. |
| `src/ToleranceTool.Configuration` | Load / save / validate configuration artifacts. |
| `src/ToleranceTool.Import` | Signal-configuration import (file + Access sources). |
| `src/ToleranceTool.Excel` | Datasheet reader/writer over the Excel object model. |
| `src/ToleranceTool.UI` | WinForms task panes and dialogs. |
| `src/ToleranceTool.AddIn` | Excel-DNA host: entry point, ribbon. Packs to the `.xll`. |
| `tests/ToleranceTool.Tests` | xUnit tests for Core / Configuration / Import / Excel. |
| `dist/` | Installer scripts, starter config libraries, sample data. |

## Build

Requires the .NET SDK (builds `net48` via the reference-assemblies package — no
Visual Studio needed).

```bash
dotnet build ToleranceTool.sln -c Debug
dotnet test  ToleranceTool.sln
```

The `ToleranceTool.AddIn` project produces (64-bit only):

| File | Use |
| --- | --- |
| `src/ToleranceTool.AddIn/bin/<Config>/net48/ToleranceTool64.xll` | Loose — needs the sibling DLLs next to it. |
| `src/ToleranceTool.AddIn/bin/<Config>/net48/publish/ToleranceTool64-packed.xll` | Single file — everything bundled. Use this to distribute. |

## Install

```powershell
dotnet build ToleranceTool.sln -c Release
powershell -ExecutionPolicy Bypass -File dist\Install-ToleranceTool.ps1
```

Per-user, no admin rights. Or load `ToleranceTool64-packed.xll` by hand via
**File → Options → Add-ins → Manage: Excel Add-ins → Go → Browse**. See the
[user guide](docs/UserGuide.md) for what to do next.

## Status

All build phases **P0–P8 complete.** Calculation engine, scale-type / signal-type /
tolerance / alias-table editors, file + Access signal import, row- and
column-oriented datasheet mapping with Apply / Check / Pass-Fail, single-file
packaged `.xll`, installer, starter config, and sample data. 111 tests.
