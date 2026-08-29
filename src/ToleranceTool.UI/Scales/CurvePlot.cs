using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ToleranceTool.Core.Scales;

namespace ToleranceTool.UI.Scales
{
    /// <summary>Plots a <see cref="ScaleCurve"/>'s Forward (and inverse-check) over [0, 1].</summary>
    public sealed class CurvePlot : Panel
    {
        private ScaleCurve? _curve;

        public CurvePlot()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            BorderStyle = BorderStyle.FixedSingle;
        }

        public void Show(ScaleCurve? curve)
        {
            _curve = curve;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle area = ClientRectangle;
            area.Inflate(-12, -12);
            if (area.Width < 20 || area.Height < 20)
            {
                return;
            }

            PointF Map(double x, double y) => new PointF(
                area.Left + (float)(x * area.Width),
                area.Bottom - (float)(y * area.Height));

            using (var axis = new Pen(Color.Gainsboro))
            {
                g.DrawRectangle(axis, area);
                g.DrawLine(axis, Map(0, 0), Map(1, 1)); // y = x reference
            }

            if (_curve == null)
            {
                TextRenderer.DrawText(g, "No curve", Font, area, Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            DrawCurve(g, area, Map, x => Safe(() => _curve!.Forward(x)), Color.RoyalBlue);
            DrawCurve(g, area, Map, x => Safe(() => _curve!.Inverse(x)), Color.SeaGreen);

            TextRenderer.DrawText(g, "Forward", Font, new Point(area.Left + 4, area.Top + 2), Color.RoyalBlue);
            TextRenderer.DrawText(g, "Inverse", Font, new Point(area.Left + 4, area.Top + 18), Color.SeaGreen);
        }

        private static void DrawCurve(Graphics g, Rectangle area, Func<double, double, PointF> map, Func<double, double> f, Color color)
        {
            const int samples = 120;
            var points = new System.Collections.Generic.List<PointF>();
            for (int i = 0; i <= samples; i++)
            {
                double x = (double)i / samples;
                double y = f(x);
                if (double.IsNaN(y) || double.IsInfinity(y) || y < -0.5 || y > 1.5)
                {
                    if (points.Count > 1)
                    {
                        g.DrawLines(new Pen(color, 2), points.ToArray());
                    }

                    points.Clear();
                    continue;
                }

                points.Add(map(x, y));
            }

            if (points.Count > 1)
            {
                using (var pen = new Pen(color, 2))
                {
                    g.DrawLines(pen, points.ToArray());
                }
            }
        }

        private static double Safe(Func<double> f)
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }
    }
}
