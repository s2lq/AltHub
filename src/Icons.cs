using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AltHub
{
    // Clean single-colour vector icons, drawn in code (no emoji, no image files). Everything is designed on a 16x16 grid.
    public static class Icons
    {
        public static void Draw(Graphics g, string name, Rectangle r, Color c)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            float s = Math.Min(r.Width, r.Height) / 16f;
            g.ScaleTransform(s, s);
            using (SolidBrush b = new SolidBrush(c))
            using (Pen p = new Pen(c, 1.6f))
            {
                p.LineJoin = LineJoin.Round;
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                switch (name)
                {
                    case "home":
                        g.FillPolygon(b, new PointF[] { new PointF(8, 1.5f), new PointF(15, 7.8f), new PointF(13, 7.8f), new PointF(13, 14.5f), new PointF(9.6f, 14.5f),
                            new PointF(9.6f, 10), new PointF(6.4f, 10), new PointF(6.4f, 14.5f), new PointF(3, 14.5f), new PointF(3, 7.8f), new PointF(1, 7.8f) });
                        break;
                    case "play":
                        g.FillPolygon(b, new PointF[] { new PointF(4, 1.8f), new PointF(14, 8), new PointF(4, 14.2f) });
                        break;
                    case "globe":
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.DrawEllipse(p, 5.2f, 1.8f, 5.6f, 12.4f);
                        g.DrawLine(p, 1.8f, 8, 14.2f, 8);
                        break;
                    case "lock":
                        using (GraphicsPath body = Round(new RectangleF(2.8f, 7, 10.4f, 7.8f), 1.8f)) g.FillPath(b, body);
                        g.DrawArc(p, 4.8f, 1.6f, 6.4f, 8.2f, 180, 180);
                        g.DrawLine(p, 4.8f, 5.7f, 4.8f, 7.4f);
                        g.DrawLine(p, 11.2f, 5.7f, 11.2f, 7.4f);
                        break;
                    case "window":
                        using (GraphicsPath w = Round(new RectangleF(1.8f, 2.4f, 12.4f, 11.2f), 2f)) g.DrawPath(p, w);
                        g.FillRectangle(b, 2.4f, 3f, 11.2f, 2.6f);
                        break;
                    case "chart":
                        g.FillRectangle(b, 2f, 9f, 3.2f, 5.5f);
                        g.FillRectangle(b, 6.4f, 5f, 3.2f, 9.5f);
                        g.FillRectangle(b, 10.8f, 1.8f, 3.2f, 12.7f);
                        break;
                    case "bookmark":
                        g.FillPolygon(b, new PointF[] { new PointF(3.6f, 1.6f), new PointF(12.4f, 1.6f), new PointF(12.4f, 14.6f), new PointF(8, 10.6f), new PointF(3.6f, 14.6f) });
                        break;
                    case "bolt":
                        g.FillPolygon(b, new PointF[] { new PointF(9.4f, 1), new PointF(3.4f, 9), new PointF(7.4f, 9), new PointF(6.4f, 15), new PointF(12.6f, 6.6f), new PointF(8.6f, 6.6f) });
                        break;
                    case "record":
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.FillEllipse(b, 5, 5, 6, 6);
                        break;
                    case "help":
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        using (Font f = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("?", f, b, new RectangleF(1, 1.3f, 14, 14), sf);
                        break;
                    case "gear":
                        for (int i = 0; i < 8; i++)
                        {
                            GraphicsState gs = g.Save();
                            g.TranslateTransform(8, 8);
                            g.RotateTransform(i * 45f);
                            g.FillRectangle(b, -1.4f, -7.4f, 2.8f, 3.6f);
                            g.Restore(gs);
                        }
                        using (GraphicsPath ring = new GraphicsPath(FillMode.Alternate))
                        {
                            ring.AddEllipse(3f, 3f, 10f, 10f);
                            ring.AddEllipse(6f, 6f, 4f, 4f);
                            g.FillPath(b, ring);
                        }
                        break;
                    case "note":
                        using (GraphicsPath n = Round(new RectangleF(2.8f, 1.8f, 10.4f, 12.4f), 1.8f)) g.DrawPath(p, n);
                        g.DrawLine(p, 5.4f, 6f, 10.6f, 6f);
                        g.DrawLine(p, 5.4f, 9f, 10.6f, 9f);
                        break;
                }
            }
            g.Restore(st);
        }

        static GraphicsPath Round(RectangleF r, float rad)
        {
            float d = rad * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
