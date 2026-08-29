using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExcelDna.Integration.CustomUI;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Scales;
using ToleranceTool.Excel;
using ToleranceTool.Excel.Datasheet;
using ToleranceTool.Import;
using ToleranceTool.UI;
using ToleranceTool.UI.Datasheet;
using ToleranceTool.UI.Import;
using ToleranceTool.UI.Scales;
using ToleranceTool.UI.SignalTypes;
using ToleranceTool.UI.Tolerances;

namespace ToleranceTool.AddIn
{
    /// <summary>
    /// The "Tolerance Tool" ribbon tab. Excel-DNA discovers this class automatically
    /// because it derives from <see cref="ExcelRibbon"/>.
    /// </summary>
    [ComVisible(true)]
    public sealed class RibbonController : ExcelRibbon
    {
        private IRibbonUI? _ribbon;

        public override string GetCustomUI(string ribbonId)
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<customUI xmlns=""http://schemas.microsoft.com/office/2009/07/customui"" onLoad=""OnLoad"">
  <ribbon>
    <tabs>
      <tab id=""ttTab"" label=""Tolerance Tool"">
        <group id=""ttSetup"" label=""Setup"">
          <button id=""ttDatasheetMapping"" label=""Datasheet Mapping"" size=""large"" imageMso=""TableProperties"" onAction=""OnSetup"" />
          <button id=""ttSignalConfiguration"" label=""Signal Configuration"" size=""large"" imageMso=""ImportSharePointList"" onAction=""OnSetup"" />
          <button id=""ttAliasTables"" label=""Alias Tables"" size=""large"" imageMso=""NameManager"" onAction=""OnSetup"" />
          <button id=""ttToleranceEditor"" label=""Tolerance Editor"" size=""large"" imageMso=""RangeProperties"" onAction=""OnSetup"" />
          <button id=""ttScaleTypes"" label=""Scale Types"" size=""large"" imageMso=""ChartTypeXYScatterChart"" onAction=""OnSetup"" />
          <button id=""ttSignalTypes"" label=""Signal Types"" size=""large"" imageMso=""ListMacros"" onAction=""OnSetup"" />
        </group>
        <group id=""ttRun"" label=""Run"">
          <button id=""ttApply"" label=""Apply Tolerances"" size=""large"" imageMso=""Calculator"" onAction=""OnApply"" />
          <button id=""ttCheck"" label=""Check Tolerances"" size=""large"" imageMso=""Refresh"" onAction=""OnCheck"" />
          <button id=""ttPassFail"" label=""Pass / Fail"" size=""large"" imageMso=""AcceptTask"" onAction=""OnPassFail"" />
          <button id=""ttClearComments"" label=""Clear Tool Comments"" size=""large"" imageMso=""ReviewDeleteComment"" onAction=""OnClearComments"" />
        </group>
        <group id=""ttStatus"" label=""Status"">
          <labelControl id=""ttStatusLabel"" getLabel=""GetStatusLabel"" />
          <button id=""ttRefreshStatus"" label=""Refresh"" imageMso=""Refresh"" onAction=""OnRefreshStatus"" />
        </group>
      </tab>
    </tabs>
  </ribbon>
</customUI>";
        }

        // ReSharper disable UnusedParameter.Global -- ribbon callback signatures are fixed.

        public void OnLoad(IRibbonUI ribbon)
        {
            _ribbon = ribbon;
        }

        public string GetStatusLabel(IRibbonControl control)
        {
            string sheet = SafeActiveSheetName();
            // Readiness checks are wired in P3/P2; show placeholders for now.
            return $"Sheet: {sheet}    Signal Config: —    Tolerance Config: —";
        }

        public void OnRefreshStatus(IRibbonControl control)
        {
            _ribbon?.InvalidateControl("ttStatusLabel");
        }

        public void OnSetup(IRibbonControl control)
        {
            switch (control.Id)
            {
                case "ttToleranceEditor":
                    ShowDialog(new ToleranceEditorForm());
                    break;

                case "ttSignalConfiguration":
                    ShowDialog(new SignalImportForm());
                    break;

                case "ttScaleTypes":
                    ShowDialog(new ScaleTypeEditorForm());
                    break;

                case "ttSignalTypes":
                    ShowDialog(new SignalTypeEditorForm());
                    break;

                case "ttAliasTables":
                    ShowDialog(new AliasTableEditorForm());
                    break;

                case "ttDatasheetMapping":
                    OpenDatasheetMapping();
                    break;

                default:
                    Placeholders.NotImplemented(SetupFeatureName(control.Id));
                    break;
            }
        }

        private void OpenDatasheetMapping()
        {
            try
            {
                var sheet = new ExcelDatasheet(ExcelApplication.ActiveSheet);
                string path = SheetMappingPath(sheet.Name);
                string? xml = File.Exists(path) ? File.ReadAllText(path) : null;
                ShowDialog(new DatasheetMappingForm(sheet, xml));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Tolerance Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowDialog(Form form)
        {
            try
            {
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Tolerance Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                form.Dispose();
            }
        }

        public void OnApply(IRibbonControl control) => RunActiveSheet(DatasheetRunMode.Apply);

        public void OnCheck(IRibbonControl control) => RunActiveSheet(DatasheetRunMode.Check);

        public void OnPassFail(IRibbonControl control) => RunActiveSheet(null);

        public void OnClearComments(IRibbonControl control)
        {
            try
            {
                new ExcelDatasheet(ExcelApplication.ActiveSheet).ClearToolComments(DatasheetRunner.CommentMarker);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Tolerance Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunActiveSheet(DatasheetRunMode? mode)
        {
            try
            {
                var sheet = new ExcelDatasheet(ExcelApplication.ActiveSheet);

                string mappingPath = SheetMappingPath(sheet.Name);
                if (!File.Exists(mappingPath))
                {
                    MessageBox.Show(
                        $"No datasheet mapping saved for \"{sheet.Name}\". Open Datasheet Mapping first.",
                        "Tolerance Tool", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DatasheetMapping mapping = DatasheetMappingXml.FromXml(File.ReadAllText(mappingPath)).Value;

                ToleranceLibrary tolerances = File.Exists(ConfigurationPaths.ToleranceLibraryFile)
                    ? ToleranceLibraryXml.Load(ConfigurationPaths.ToleranceLibraryFile).Value
                    : new ToleranceLibrary();

                AliasTableSet aliases = File.Exists(ConfigurationPaths.AliasTablesFile)
                    ? AliasTablesXml.Load(ConfigurationPaths.AliasTablesFile).Value
                    : AliasTableSet.Empty();

                string sidecar = Path.Combine(ConfigurationPaths.RootFolder, "last-signal-set.xml");
                var signals = File.Exists(sidecar)
                    ? SignalConfigSetXml.Load(sidecar).Value
                    : new System.Collections.Generic.List<Core.Signals.SignalConfig>();

                ScaleCurveLibrary curves = LoadCurves();

                var resolver = new SignalResolver(signals, aliases, mapping.ResolutionOverrides);
                var runner = new DatasheetRunner(resolver, tolerances, curves);
                DatasheetRunResult result = mode.HasValue
                    ? runner.Run(sheet, mapping, mode.Value)
                    : runner.RunPassFail(sheet, mapping);

                string title = mode.HasValue ? mode.Value.ToString() : "Pass / Fail";
                MessageBox.Show(result.Summary(), $"{title} — {sheet.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Tolerance Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ScaleCurveLibrary LoadCurves()
        {
            try
            {
                if (File.Exists(ConfigurationPaths.ScaleTypeLibraryFile))
                {
                    var loaded = ToleranceTool.Configuration.Scales.ScaleTypeLibraryXml.Load(ConfigurationPaths.ScaleTypeLibraryFile);
                    if (!loaded.HasErrors && loaded.Value.Count > 0)
                    {
                        return ScaleCurveLibrary.From(loaded.Value);
                    }
                }
            }
            catch
            {
                // fall through
            }

            return ScaleCurveLibrary.CreateDefault();
        }

        private static string SheetMappingPath(string sheetName)
        {
            string safe = string.Concat(sheetName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(ConfigurationPaths.RootFolder, "sheets", safe + ".xml");
        }

        // ReSharper restore UnusedParameter.Global

        private static string SafeActiveSheetName()
        {
            try
            {
                string name = ExcelApplication.ActiveSheetName;
                return string.IsNullOrEmpty(name) ? "(none)" : name;
            }
            catch
            {
                return "(none)";
            }
        }

        private static string SetupFeatureName(string controlId)
        {
            switch (controlId)
            {
                case "ttDatasheetMapping": return "Datasheet Mapping";
                case "ttSignalConfiguration": return "Signal Configuration";
                case "ttAliasTables": return "Alias Tables";
                case "ttToleranceEditor": return "Tolerance Editor";
                case "ttScaleTypes": return "Scale Types";
                case "ttSignalTypes": return "Signal Types";
                default: return controlId;
            }
        }
    }
}
