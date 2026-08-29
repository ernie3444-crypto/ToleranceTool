# ToleranceTool.Wpf

Proof-of-concept WPF re-implementation of the `ToleranceTool.UI` WinForms screens,
on the `wpf-ui` branch.

**Why:** the WinForms editors needed a pile of `OnLoad` `SplitContainer` workarounds
and manual visibility toggling to lay out correctly. WPF's layout panels, styles and
data binding remove that class of bug. `Core` / `Configuration` / `Import` / `Excel`
are untouched — only the UI layer and the ribbon wiring change.

## Status

| Screen | State |
|---|---|
| Scale Type Editor | **ported** — `Scales/ScaleTypeEditorWindow.xaml` + `ScaleTypeEditorViewModel` |
| everything else | still WinForms (`ToleranceTool.UI`) |

The ribbon's **Scale Types** button opens the WPF window
(`RibbonController` → `WpfDialogs.ScaleTypeEditor`). All other buttons still open the
WinForms forms.

## How it's wired

- SDK-style project, `net48`, `<UseWPF>true</UseWPF>` (XAML compiles under `dotnet build`,
  no Visual Studio needed).
- `WpfDialogs` is the single entry point the add-in calls; it parents the window to
  Excel's HWND via `WindowInteropHelper`.
- `ToleranceTool.Wpf.dll` is packed into the single-file `.xll` (listed in `ToleranceTool.dna`).
  WPF's framework assemblies (`PresentationFramework` etc.) ship with .NET Framework, so
  they are not packed.

## Pattern

Tiny MVVM: `Mvvm/ObservableObject`, `Mvvm/RelayCommand`. One view model per screen; the
view models call the same `Configuration` XML loaders/validators the WinForms code did.
