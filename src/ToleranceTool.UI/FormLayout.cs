using System;
using System.Windows.Forms;

namespace ToleranceTool.UI
{
    /// <summary>Shared WinForms layout helpers.</summary>
    internal static class FormLayout
    {
        /// <summary>
        /// Sets a <see cref="SplitContainer"/>'s splitter to a fraction of its
        /// current extent, clamped to the panel minimums. Call from
        /// <c>OnLoad</c> — setting <c>SplitterDistance</c> in an object initializer
        /// on a not-yet-sized container is silently ignored.
        /// </summary>
        public static void SetSplit(SplitContainer split, double fraction, int panel1Min = -1, int panel2Min = -1)
        {
            if (split == null)
            {
                return;
            }

            try
            {
                int extent = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
                if (extent <= 0)
                {
                    return;
                }

                // Setting the minimums re-validates SplitterDistance, so do it now (once
                // the container is sized) rather than in an object initializer.
                if (panel1Min >= 0)
                {
                    split.Panel1MinSize = Math.Min(panel1Min, Math.Max(0, extent - 40));
                }

                if (panel2Min >= 0)
                {
                    split.Panel2MinSize = Math.Min(panel2Min, Math.Max(0, extent - split.Panel1MinSize - 20));
                }

                int distance = (int)(extent * fraction);
                distance = Math.Max(split.Panel1MinSize, Math.Min(distance, extent - split.Panel2MinSize));
                if (distance > 0 && distance < extent)
                {
                    split.SplitterDistance = distance;
                }
            }
            catch (InvalidOperationException)
            {
                // container smaller than its minimums at load time — the default split is acceptable
            }
        }
    }
}
