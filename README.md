# Tolerance Tool

An Excel add-in that standardizes tolerance calculations on engineering test
datasheets. Built on **Excel-DNA**, **C# / .NET Framework 4.8**, **64-bit Excel**.

See [`docs/ToleranceTool-Architecture.html`](docs/ToleranceTool-Architecture.html)
for the full design and the phased build plan.

## Solution layout

| Project | Role |
| --- | --- |
| `src/ToleranceTool.Core` | Domain model + calculation engine. No Excel, no I/O. |
| `src/ToleranceTool.Configuration` | Load / save / validate configuration artifacts. |
| `src/ToleranceTool.Import` | Signal-configuration import (file + Access sources). |
| `src/ToleranceTool.Excel` | Datasheet reader/writer over the Excel object model. |
| `src/ToleranceTool.UI` | WinForms task panes and dialogs. |
| `src/ToleranceTool.AddIn` | Excel-DNA host: entry point, ribbon. Packs to the `.xll`. |
| `tests/ToleranceTool.Tests` | xUnit tests for Core / Configuration / Import. |

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

## Load in Excel

1. Build the solution.
2. In Excel: **File → Options → Add-ins → Manage: Excel Add-ins → Go → Browse**,
   select `ToleranceTool64-packed.xll`.
3. A **Tolerance Tool** tab appears on the ribbon. (P0: buttons report
   "not implemented yet".)

## Status

**P0 — solution scaffold.** Projects build, tests pass, the ribbon loads.
Next: **P1 — calculation engine + scale curves.**
