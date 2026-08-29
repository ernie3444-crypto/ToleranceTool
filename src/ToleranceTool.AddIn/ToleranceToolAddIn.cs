using ExcelDna.Integration;

namespace ToleranceTool.AddIn
{
    /// <summary>
    /// Excel-DNA entry point. Runs when the .xll is loaded / unloaded.
    /// </summary>
    public sealed class ToleranceToolAddIn : IExcelAddIn
    {
        public void AutoOpen()
        {
            // P0: nothing to wire up yet. Feature registration lands here from P2 on.
        }

        public void AutoClose()
        {
        }
    }
}
