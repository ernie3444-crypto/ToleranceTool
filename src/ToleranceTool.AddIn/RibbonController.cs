using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExcelDna.Integration.CustomUI;
using ToleranceTool.Excel;
using ToleranceTool.UI;
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

                default:
                    Placeholders.NotImplemented(SetupFeatureName(control.Id));
                    break;
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

        public void OnApply(IRibbonControl control)
        {
            Placeholders.NotImplemented("Apply Tolerances");
        }

        public void OnCheck(IRibbonControl control)
        {
            Placeholders.NotImplemented("Check Tolerances");
        }

        public void OnClearComments(IRibbonControl control)
        {
            Placeholders.NotImplemented("Clear Tool Comments");
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
