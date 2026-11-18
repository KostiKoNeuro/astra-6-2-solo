using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace TaskbarTails
{
    internal sealed class Art : IDisposable
    {
        readonly Dictionary<string, Bitmap> sprites = new Dictionary<string, Bitmap>();
        public readonly Bitmap Clearing;
        public readonly Icon Icon;
        static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream("Art." + name); }
        static Bitmap Load(string file)
        {
            using (Stream stream = Resource(file))
            using (Image image = Image.FromStream(stream)) return new Bitmap(image);
        }
        public Art()
        {
            foreach (string s in new[] {"stride","step","lift","nap","biscuit","scout","count","debug","read"})
                sprites[s] = Load(s + ".png");
            Clearing = Load("clearing.jpg");
            using (Stream stream = Resource("pip.ico"))
            using (Icon input = new Icon(stream, 32, 32)) Icon = (Icon)input.Clone();
        }
        public Bitmap this[string key] { get { return sprites[key]; } }
        public void DrawPip(Graphics g, Pip pip, float center, float floor, float scale, bool orient)
        {
            Bitmap im = this[pip.Sprite];
            float w = im.Width * .272f * scale, h = im.Height * .272f * scale;
            double angle = 0, bob = 0, stretch = 1;
            switch (pip.State)
            {
                case Mood.Patrol:
                    double stride = pip.Gait * Math.PI * 4.4;
                    bob = 1.75 * Math.Abs(Math.Sin(stride));
                    angle = Math.Sin(stride) * 1.8;
                    break;
                case Mood.Flight:
                    angle = Pip.Clamp(pip.Vx / pip.Scale / 26, -40, 40) + Math.Sin(pip.Age * 8) * 6;
                    break;
                case Mood.Held: angle = 7 * Math.Sin(pip.Age * 4); break;
                case Mood.Biscuit: stretch += .026 * Math.Sin(pip.Age * 13); break;
                case Mood.Nap: stretch += .026 * Math.Sin(pip.Age * 2.1); break;
                case Mood.Petted: angle = 4 * Math.Sin(pip.Age * 8); break;
                default: stretch += .012 * Math.Sin(pip.Age * 3.7); break;
            }
            double after = pip.Time - pip.LandedAt;
            if (after < .22) stretch -= .12 * Math.Sin(Math.PI * after / .22);
            GraphicsState saved = g.Save();
            g.TranslateTransform(center, floor - h / 2 - (float)bob * scale);
            g.RotateTransform((float)angle);
            g.ScaleTransform(orient && !pip.FacesRight ? -1 : 1, 1);
            float hh = h * (float)stretch, ww = w / (float)stretch;
            g.DrawImage(im, -ww / 2, h / 2 - hh, ww, hh);
            g.Restore(saved);
        }
        public void Dispose()
        {
            foreach (Bitmap b in sprites.Values) b.Dispose();
            Clearing.Dispose(); Icon.Dispose();
        }
    }

    internal static class Ink
    {
        public static readonly Color Paper = Color.FromArgb(249,247,238), Cream = Color.FromArgb(255,253,245),
            Forest = Color.FromArgb(38,63,48), Dark = Color.FromArgb(42,52,43), Muted = Color.FromArgb(95,105,89),
            Sage = Color.FromArgb(225,233,212), Line = Color.FromArgb(211,212,192), Rust = Color.FromArgb(167,78,37),
            Honey = Color.FromArgb(230,174,91);
        public static void Prepare(Graphics g)
        {
            g.PageUnit = GraphicsUnit.Pixel;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        public static GraphicsPath Round(float x,float y,float w,float h,float r)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(Math.Min(w,h),r*2);
            path.AddArc(x,y,d,d,180,90); path.AddArc(x+w-d,y,d,d,270,90);
            path.AddArc(x+w-d,y+h-d,d,d,0,90); path.AddArc(x,y+h-d,d,d,90,90);
            path.CloseFigure();
            return path;
        }
        public static void Box(Graphics g, float x, float y, float w, float h, float r, Color fill, Color border)
        {
            using (GraphicsPath path = Round(x,y,w,h,r))
            {
                using (Brush b = new SolidBrush(fill)) g.FillPath(b,path);
                if (border.A > 0) using (Pen p = new Pen(border)) g.DrawPath(p,path);
            }
        }
        public static void Text(Graphics g, string s, float x, float y, float w, float h, float size,
            Color color, string face = "Segoe UI", FontStyle style = FontStyle.Regular,
            StringAlignment align = StringAlignment.Near, bool wrap = false)
        {
            using (Font f = new Font(face,size,style,GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(color))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = align;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(s,f,b,new RectangleF(x,y,w,h),format);
            }
        }
        public static void Rule(Graphics g, Color color, float x, float y, float x2, float y2)
        {
            using (Pen p = new Pen(color)) g.DrawLine(p,x,y,x2,y2);
        }
        // Hand-set little wayfinding marks, independent of Windows' installed symbol fonts.
        public static void Glyph(Graphics g, string kind, float x, float y, float size, Color c)
        {
            GraphicsState save = g.Save();
            g.TranslateTransform(x,y); g.ScaleTransform(size/24,size/24);
            using (Pen p = new Pen(c,1.65f))
            using (Brush b = new SolidBrush(c))
            {
                p.StartCap = p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                switch (kind)
                {
                    case "paw":
                        g.FillEllipse(b,2,7,4.5f,6); g.FillEllipse(b,7,3,4.3f,6);
                        g.FillEllipse(b,12.8f,2.8f,4.3f,6); g.FillEllipse(b,17.8f,6.7f,4.3f,5.8f);
                        using (GraphicsPath pad = new GraphicsPath())
                        {
                            pad.AddBezier(5,17,6.8f,14.3f,8,10,12,10);
                            pad.AddBezier(12,10,15.5f,10,18.4f,15,19.2f,17.5f);
                            pad.AddBezier(19.2f,17.5f,20,23,15,20.5f,12,20);
                            pad.AddBezier(12,20,9,20.3f,3.2f,22.7f,5,17); g.FillPath(b,pad);
                        } break;
                    case "biscuit":
                        g.DrawEllipse(p,3,3,18,18);
                        g.FillEllipse(b,8,8,2.8f,2.8f); g.FillEllipse(b,14.6f,9.4f,2.3f,2.3f);
                        g.FillEllipse(b,11,15,2.6f,2.6f); break;
                    case "moon":
                        using (GraphicsPath moon = new GraphicsPath())
                        {
                            moon.AddBezier(15,3,7,1,1,10,5,16);
                            moon.AddBezier(5,16,9,23,20,21,21,13);
                            moon.AddBezier(21,13,14,17,9,8,15,3); g.DrawPath(p,moon);
                        } break;
                    case "sun":
                        g.DrawEllipse(p,7,7,10,10);
                        for (int i=0;i<8;i++) { double a=i*Math.PI/4; g.DrawLine(p,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10); } break;
                    case "book":
                        g.DrawLines(p,new PointF[] {new PointF(12,7),new PointF(8,4.5f),new PointF(2.5f,4),new PointF(2.5f,18),new PointF(8,18.5f),new PointF(12,21),new PointF(16,18.5f),new PointF(21.5f,18),new PointF(21.5f,4),new PointF(16,4.5f),new PointF(12,7),new PointF(12,21)}); break;
                    case "exit":
                        g.DrawLines(p,new Point[]{new Point(12,4),new Point(4,4),new Point(4,20),new Point(12,20)});
                        g.DrawLine(p,9,12,21,12); g.DrawLine(p,17,8,21,12); g.DrawLine(p,17,16,21,12); break;
                    case "close": g.DrawLine(p,6,6,18,18); g.DrawLine(p,18,6,6,18); break;
                    case "arrow": g.DrawLine(p,6,18,18,6); g.DrawLine(p,8,6,18,6); g.DrawLine(p,18,6,18,16); break;
                    case "leaf":
                        g.DrawBezier(p,3,19,2,4,12,5,21,3); g.DrawBezier(p,21,3,21,13,17,22,3,19);
                        g.DrawLine(p,3,21,16,9); break;
                    case "mouse":
                        using (GraphicsPath m=Round(5,2,14,20,7)) g.DrawPath(p,m);
                        g.DrawLine(p,12,5,12,9); break;
                    case "clock": g.DrawEllipse(p,3,3,18,18); g.DrawLine(p,12,6.5f,12,12); g.DrawLine(p,12,12,16,14); break;
                }
            }
            g.Restore(save);
        }
    }
}
