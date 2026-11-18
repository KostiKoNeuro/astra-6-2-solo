using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TaskbarTails
{
    // A top-down premultiplied DIB is shared by GDI+ and UpdateLayeredWindow.
    // Windows itself excludes zero-alpha pixels from hit testing, across processes.
    internal sealed class Overlay : NativeWindow, IDisposable
    {
        IntPtr dc, dib, previous;
        Bitmap bitmap;
        Graphics graphics;
        bool shown;
        public Rectangle Bounds { get; private set; }
        public delegate bool MessageHandler(ref Message msg);
        public MessageHandler Handler;
        public Overlay(string title, bool ghost)
        {
            CreateHandle(new CreateParams {
                Caption = title, Style = Win.WS_POPUP,
                ExStyle = Win.LAYERED | Win.NOACTIVATE | Win.TOOLWINDOW | Win.TOPMOST | (ghost ? Win.TRANSPARENT : 0)
            });
        }
        void ReleaseSurface()
        {
            if (graphics != null) { graphics.Dispose(); graphics = null; }
            if (bitmap != null) { bitmap.Dispose(); bitmap = null; }
            if (dc != IntPtr.Zero)
            {
                Win.SelectObject(dc, previous);
                Win.DeleteObject(dib);
                Win.DeleteDC(dc);
                dc = IntPtr.Zero;
            }
        }
        public void Paint(Rectangle bounds, Action<Graphics> draw, byte opacity)
        {
            if (bitmap == null || bounds.Size != bitmap.Size)
            {
                ReleaseSurface();
                Win.DibInfo info = new Win.DibInfo { Size = 40, Width = bounds.Width,
                    Height = -bounds.Height, Planes = 1, BitCount = 32 };
                dc = Win.CreateCompatibleDC(IntPtr.Zero);
                IntPtr bits;
                dib = Win.CreateDIBSection(dc, ref info, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                previous = Win.SelectObject(dc,dib);
                bitmap = new Bitmap(bounds.Width,bounds.Height,bounds.Width*4,PixelFormat.Format32bppPArgb,bits);
                graphics = Graphics.FromImage(bitmap);
                Ink.Prepare(graphics);
            }
            Bounds = bounds;
            graphics.ResetTransform();
            graphics.Clear(Color.Transparent);
            GraphicsState save = graphics.Save();
            draw(graphics);
            graphics.Restore(save);
            graphics.Flush(FlushIntention.Sync);
            Win.Pt point = new Win.Pt(bounds.Location), origin = new Win.Pt();
            Win.Dim dim = new Win.Dim(bounds.Width,bounds.Height);
            Win.Blend blend = new Win.Blend { Opacity = opacity, Alpha = 1 };
            if (!Win.UpdateLayeredWindow(Handle,IntPtr.Zero,ref point,ref dim,dc,ref origin,0,ref blend,2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!shown)
            {
                Win.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x53); // SHOWWINDOW | NOACTIVATE | NOMOVE | NOSIZE
                shown = true;
            }
        }
        public void Hide()
        {
            if (shown) Win.ShowWindow(Handle,0);
            shown = false;
        }
        public int AlphaAt(int x, int y) { return bitmap.GetPixel(x,y).A; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win.MOUSEACTIVATE) { m.Result = new IntPtr(3); return; }
            if (m.Msg == 0x14) { m.Result = new IntPtr(1); return; }
            if (Handler == null || !Handler(ref m)) base.WndProc(ref m);
        }
        public void Dispose()
        {
            Handler = null;
            Hide();
            ReleaseSurface();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }

    internal sealed class PetView : IDisposable
    {
        public const string WindowTitle = "Taskbar Tails | Pip the raccoon";
        public readonly Overlay Pet = new Overlay(WindowTitle,false);
        public readonly Overlay Speech = new Overlay("Pip's field notes",true);
        readonly Art art;
        public PetView(Art art) { this.art = art; }
        public void Render(Pip pip)
        {
            float s = (float)pip.Scale;
            int w = (int)Math.Ceiling(162*s), h = (int)Math.Ceiling(148*s);
            Rectangle bounds = new Rectangle((int)Math.Round(pip.X-w/2f),(int)Math.Round(pip.Y-h+4*s),w,h);
            Pet.Paint(bounds,delegate(Graphics g) { art.DrawPip(g,pip,w/2f,h-4*s,s,true); },255);
            if (pip.State == Mood.Nap) DrawZs(pip,s);
            else if (pip.LineUntil > pip.Time && !String.IsNullOrEmpty(pip.Line)) DrawLine(pip,s);
            else Speech.Hide();
        }
        void DrawZs(Pip pip, float s)
        {
            int w = (int)Math.Ceiling(63*s), h=(int)Math.Ceiling(52*s);
            int x = (int)Pip.Clamp(pip.X+(pip.FacesRight?20:-72)*s,pip.Meadow.Left,pip.Meadow.Right-w);
            Speech.Paint(new Rectangle(x,(int)(pip.Y-123*s),w,h), delegate(Graphics g)
            {
                g.ScaleTransform(s,s);
                for (int i=0;i<3;i++)
                {
                    double phase = (pip.Age*.32+i/3.0)%1;
                    float px=(float)(3+phase*31), py=(float)(29-phase*29), sz=(float)(10+phase*7);
                    int alpha = (int)(235*Math.Sin(phase*Math.PI));
                    Ink.Box(g,px,py,sz+7,sz+7,7,Color.FromArgb(alpha,Ink.Paper),Color.Transparent);
                    Ink.Text(g,"z",px,py-1,sz+7,sz+7,sz,Color.FromArgb(alpha,Ink.Forest),"Georgia",FontStyle.Italic,StringAlignment.Center);
                }
            },255);
        }
        void DrawLine(Pip pip, float s)
        {
            const int height = 41;
            int width;
            using (Font f = new Font("Segoe UI",13,FontStyle.Regular,GraphicsUnit.Pixel))
                width = TextRenderer.MeasureText(pip.Line,f,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width + 33;
            int w = (int)Math.Ceiling(width*s), h=(int)Math.Ceiling(height*s);
            double fade = Pip.Clamp((pip.LineUntil-pip.Time)*4,0,1);
            float bodyH = art[pip.Sprite].Height*.272f*s;
            int x = (int)Pip.Clamp(pip.X - w/2,pip.Meadow.Left+3,pip.Meadow.Right-w-3);
            int y = (int)Math.Max(pip.Meadow.Top,pip.Y-bodyH-h-6*s);
            Speech.Paint(new Rectangle(x,y,w,h),delegate(Graphics g)
            {
                g.ScaleTransform(s,s);
                float tail = (float)Pip.Clamp((pip.X-x)/s+10,12,width-18);
                PointF[] shape = { new PointF(tail-7,29),new PointF(tail+2,39),new PointF(tail+5,29) };
                using (Brush b=new SolidBrush(Ink.Cream)) g.FillPolygon(b,shape);
                using (Pen p=new Pen(Ink.Forest,1.3f)) g.DrawLines(p,shape);
                Ink.Box(g,1,1,width-2,30,12,Ink.Cream,Ink.Forest);
                using (Brush b=new SolidBrush(Ink.Cream)) g.FillRectangle(b,tail-4.8f,29,9,3);
                using (Brush b=new SolidBrush(Ink.Rust)) g.FillEllipse(b,12,13,5,5);
                Ink.Text(g,pip.Line,23,0,width-27,31,13,Ink.Dark);
            },(byte)(255*fade));
        }
        public void Dispose() { Speech.Dispose(); Pet.Dispose(); }
    }
}
