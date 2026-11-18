using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Taskbar Tails")]
[assembly: AssemblyProduct("Taskbar Tails")]
[assembly: AssemblyDescription("Pip, a curious little raccoon for your desktop")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: InternalsVisibleTo("TaskbarTails.Checks")]

namespace TaskbarTails
{
    internal sealed class Companion : ApplicationContext
    {
        public readonly Pip Pip;
        public readonly Art Art;
        public readonly PetView View;
        public readonly TrayIcon Tray;
        public readonly FieldGuide Guide;
        public readonly CareMenu Menu;
        public bool ListenerInstalled { get; private set; }
        public bool QuitRequested { get; private set; }
        public static readonly uint OpenGuide = Win.RegisterWindowMessage("TaskbarTails.Pip.FieldGuide.v1");
        readonly uint explorerReady = Win.RegisterWindowMessage("TaskbarCreated");
        readonly IDesk desk;
        readonly System.Windows.Forms.Timer timer;
        double previous, terrainAt, trayAt, nextRead, nextGuideFrame;
        uint revision;
        int attempts;
        bool disposed;
        string lastActivity;
        public Companion(IDesk input,bool realTime)
        {
            desk=input;
            Rectangle work=Screen.PrimaryScreen.WorkingArea;
            Point middle=new Point(work.Left+work.Width/2,work.Top+work.Height/2);
            Pip=new Pip(work,Win.ScaleAt(middle));
            Art=new Art(); View=new PetView(Art);
            Guide=new FieldGuide(Pip,Art,RunCommand); Menu=new CareMenu(Pip,Art,RunCommand);
            Tray=new TrayIcon(View.Pet.Handle,Art.Icon);
            View.Pet.Handler=Handle;
            revision=desk.ClipboardRevision;
            ListenerInstalled=Win.AddClipboardFormatListener(View.Pet.Handle);
            previous=desk.Now;
            View.Render(Pip);
            timer=new System.Windows.Forms.Timer { Interval=32 };
            timer.Tick+=delegate{Tick();};
            if(realTime) timer.Start();
        }
        public void RunCommand(Command c)
        {
            if (QuitRequested) return;
            switch(c)
            {
                case Command.Feed: Pip.Feed();break;
                case Command.Bed: if(Pip.State==Mood.Nap) Pip.Wake();else Pip.Sleep(true);break;
                case Command.Guide: Guide.Present(new Point((int)Pip.X,(int)Pip.Y-1),true);break;
                case Command.Exit: Stop();return;
                case Command.Link: case Command.Number: case Command.Code: case Command.Story:
                    Pip.Find(Finds.Classify(Finds.Samples[(int)c-(int)Command.Link]));break;
            }
            if(Pip.State!=Mood.Held && Win.GetCapture()==View.Pet.Handle) Win.ReleaseCapture();
            Redraw();
        }
        void Stop()
        {
            QuitRequested=true;
            if(timer!=null) timer.Stop();
            ExitThread();
        }
        void ToggleMenu(Point where)
        {
            if(Menu.Visible) Menu.Hide();
            else Menu.Present(where,false);
        }
        bool Handle(ref Message msg)
        {
            if((uint)msg.Msg==explorerReady) { Tray.Add();return true; }
            if((uint)msg.Msg==OpenGuide) { RunCommand(Command.Guide);return true; }
            switch(msg.Msg)
            {
                case Win.TRAY:
                    int click=(int)(msg.LParam.ToInt64()&0xffff);
                    if(click==Win.LBUTTONUP || click==Win.RBUTTONUP || click==0x7b || click==0x401)
                        ToggleMenu(desk.Pointer);
                    return true;
                case Win.CLIPBOARDUPDATE:
                    uint seq=desk.ClipboardRevision;
                    if(seq!=revision)
                    {
                        revision=seq;
                        attempts=12;
                        nextRead=desk.Now;
                    }
                    return true;
                case Win.LBUTTONDOWN:
                    if(Menu.Visible) Menu.Hide();
                    Pip.Grab(ToScreen(msg.LParam),desk.Now);
                    Win.SetCapture(View.Pet.Handle);
                    Redraw();
                    return true;
                case Win.MOUSEMOVE:
                    if(Pip.State==Mood.Held) MoveGrip(ToScreen(msg.LParam));
                    return true;
                case Win.LBUTTONUP:
                    if(Pip.State==Mood.Held) Release(ToScreen(msg.LParam));
                    return true;
                case Win.RBUTTONUP: ToggleMenu(ToScreen(msg.LParam));return true;
                case 0x20: // WM_SETCURSOR
                    Cursor.Current=Pip.State==Mood.Held?Cursors.SizeAll:Cursors.Hand; return true;
                case Win.DISPLAYCHANGE: case 0x1a: case 0x02E0: terrainAt=0;return true;
                case Win.CLOSE: Stop();return true;
                case 0x11: msg.Result=new IntPtr(1);return true;
                case 0x16: if(msg.WParam!=IntPtr.Zero) Stop();return true;
            }
            return false;
        }
        Point ToScreen(IntPtr lp)
        {
            Win.Pt p=new Win.Pt(Win.MessagePoint(lp));
            Win.ClientToScreen(View.Pet.Handle,ref p);
            return p.Point;
        }
        void TerrainAt(Point p)
        {
            Rectangle work=Screen.FromPoint(p).WorkingArea;
            Pip.Terrain(work,Win.ScaleAt(p));
        }
        void MoveGrip(Point p) { TerrainAt(p); Pip.Drag(p,desk.Now); }
        void Release(Point p)
        {
            MoveGrip(p);
            Pip.Release(p,desk.Now);
            if(Win.GetCapture()==View.Pet.Handle) Win.ReleaseCapture();
            Redraw();
        }
        public void Tick()
        {
            if(disposed || QuitRequested) return;
            double now=desk.Now;
            double dt=Pip.Clamp(now-previous,0,.12);
            previous=now;
            if(Pip.State==Mood.Held)
            {
                if(desk.LeftDown) MoveGrip(desk.Pointer);
                else Release(desk.Pointer);
            }
            else if(now>=terrainAt)
            {
                TerrainAt(new Point((int)Pip.X,(int)Pip.Y-1));
                terrainAt=now+1;
            }
            if(attempts>0 && now>=nextRead)
            {
                FindKind find;
                if(desk.TryReadFind(out find)) { attempts=0;Pip.Find(find); }
                else { attempts--; nextRead=now+.08; }
            }
            uint idle=desk.IdleMs;
            // Substeps keep collision energy stable across slow frames.
            while(dt>.000001) { double step=Math.Min(.016,dt);Pip.Advance(step,idle);dt-=step; }
            if(Menu.Visible && (desk.LeftDown || Win.GetAsyncKeyState(2)<0) && !Menu.Bounds.Contains(desk.Pointer))
                Menu.Hide();
            if((Menu.Visible || Guide.Visible) && Win.GetAsyncKeyState(27)<0)
            {
                if(Menu.Visible) Menu.Hide(); else Guide.Hide();
            }
            if(!Tray.Installed && now>=trayAt) {Tray.Add();trayAt=now+5;}
            timer.Interval=Pip.State==Mood.Nap?90:32;
            Redraw();
        }
        void Redraw()
        {
            View.Render(Pip);
            bool changed=lastActivity!=Pip.Activity;
            if(changed)
            {
                lastActivity=Pip.Activity;Tray.SetStatus(lastActivity);
                if(Menu.Visible) Menu.Invalidate();
            }
            if(Guide.Visible && (changed || desk.Now>=nextGuideFrame))
            { Guide.Invalidate();nextGuideFrame=desk.Now+.085; }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && !disposed)
            {
                disposed=true;
                timer.Stop();timer.Dispose();
                if(ListenerInstalled) Win.RemoveClipboardFormatListener(View.Pet.Handle);
                ListenerInstalled=false;
                if(Win.GetCapture()==View.Pet.Handle) Win.ReleaseCapture();
                Tray.Dispose();Menu.Dispose();Guide.Dispose();View.Dispose();Art.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using (Mutex singleton=new Mutex(true,@"Local\TaskbarTails.Pip.v1",out first))
            {
                if(!first)
                {
                    IntPtr pet=Win.FindWindow(null,PetView.WindowTitle);
                    if(pet!=IntPtr.Zero) Win.PostMessage(pet,Companion.OpenGuide,IntPtr.Zero,IntPtr.Zero);
                    return 0;
                }
                using(Companion app=new Companion(new Desktop(),true))
                {
                    if(Array.IndexOf(args,"--guide")>=0) app.RunCommand(Command.Guide);
                    Application.Run(app);
                }
            }
            return 0;
        }
    }
}
