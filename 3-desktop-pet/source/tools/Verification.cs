using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TaskbarTails
{
    // All input to the interactive checks comes from this fixture. Window messages target
    // only our own HWNDs. The test never moves the system pointer or sends keyboard input.
    internal sealed class FixtureDesk : IDesk
    {
        public double Now {get;set;}
        public uint IdleMs {get;set;}
        public Point Pointer {get;set;}
        public bool LeftDown {get;set;}
        public uint ClipboardRevision {get;set;}
        public string Sample;
        public int BusyReads, Reads;
        public bool TryReadFind(out FindKind result)
        {
            Reads++;
            result=FindKind.None;
            if(BusyReads>0){BusyReads--;return false;}
            result=Finds.Classify(Sample);
            return true;
        }
        public void Step(Companion app,double seconds)
        {
            Now+=seconds;app.Tick();
        }
        public void Copy(Companion app,string s)
        {
            Sample=s;ClipboardRevision++;
            Win.SendMessage(app.View.Pet.Handle,Win.CLIPBOARDUPDATE,IntPtr.Zero,IntPtr.Zero);
            Step(app,.032);
        }
    }

    internal static class Verification
    {
        static readonly List<string> log=new List<string>();
        static void Check(bool ok,string name)
        {
            if(!ok) throw new InvalidOperationException(name);
            log.Add("PASS  "+name);
        }
        static IntPtr Pack(Point p) {return new IntPtr((p.X&0xffff)|((p.Y&0xffff)<<16));}
        static void Click(PaperWindow form,Command c)
        {
            IntPtr pos=Pack(form.ButtonCenter(c));
            Win.SendMessage(form.Handle,Win.LBUTTONDOWN,new IntPtr(1),pos);
            Win.SendMessage(form.Handle,Win.LBUTTONUP,IntPtr.Zero,pos);
        }
        static void TrayMenu(Companion app,FixtureDesk d)
        {
            d.Pointer=new Point(app.Pip.Meadow.Right-20,app.Pip.Meadow.Bottom-4);
            Win.SendMessage(app.View.Pet.Handle,Win.TRAY,new IntPtr(1),new IntPtr(Win.RBUTTONUP));
            if(!app.Menu.Visible) throw new InvalidOperationException("Tray menu did not open");
        }
        static void PetMessage(Companion app,int message,Point screen)
        {
            Rectangle b=app.View.Pet.Bounds;
            Win.SendMessage(app.View.Pet.Handle,message,IntPtr.Zero,Pack(new Point(screen.X-b.X,screen.Y-b.Y)));
        }
        static void Classifier()
        {
            Check(Finds.Classify(" \r\nhttps://example.org/f?x=2#leaf  ")==FindKind.Link && Finds.Classify("www.example.com")==FindKind.Link,"Link recognition, including a bare www address");
            Check(new[]{"42","-1,024.5","+2e-3",".25","12%","0x2A"}.All(s=>Finds.Classify(s)==FindKind.Number),"Integer, signed, grouped, decimal, exponent and hexadecimal numbers");
            Check(new[]{"def sniff(crumb):\n    return crumb","console.log(42);","{\"paws\": 4}","SELECT paws FROM raccoons","<p>forest</p>","const x = 7;","if (x) { hop(); }"}.All(s=>Finds.Classify(s)==FindKind.Code),"Python, JavaScript, JSON, SQL and markup recognition");
            Check(Finds.Classify(new string('a',159))==FindKind.None && Finds.Classify(new string('a',160))==FindKind.LongText &&
                Finds.Classify("const x = '"+new string('a',190)+"';")==FindKind.Code,"Long-text threshold and code precedence");
            Check(new[]{null,"","  ","a short note","I have a class today.","version 1.20","file:///C:/notes.txt"}.All(s=>Finds.Classify(s)==FindKind.None),"Ordinary short text and unsupported content are quiet");
        }
        static void Model()
        {
            Pip p=new Pip(new Rectangle(-1600,0,1600,900),1.5);
            double start=p.X;p.Advance(.1,0);
            Check(p.X>start && p.Y==900,"Patrol follows the work area in physical monitor coordinates");
            p.X=-90;p.Advance(.12,0);p.Advance(.12,0);
            Check(!p.FacesRight && p.X<-87,"Walker turns and mirrors at the right edge");
            p.X=-1512;p.Advance(.12,0);
            Check(p.FacesRight,"Walker turns at the left edge on a negative-coordinate monitor");
            p.Terrain(new Rectangle(-1600,0,1600,840),1.5);
            Check(p.Y==840,"Pet follows a relocated taskbar");
            p.Grab(new Point((int)p.X,800),0);
            p.Drag(new Point(-1420,560),.04);p.Drag(new Point(-1420,560),.30);p.Release(new Point(-1420,560),.33);
            Check(p.State==Mood.Flight && p.Vx==0 && p.Vy==0,"Holding still before release produces a drop");
            p.Find(FindKind.Link);p.Find(FindKind.Number);
            for(int i=0;i<300 && p.State==Mood.Flight;i++)p.Advance(.016,0);
            Check(p.State==Mood.Count && p.Y==840,"Latest copy during a toss is performed after landing");
        }
        static void NativeChecks(string plate)
        {
            IntPtr foreground=Win.GetForegroundWindow();
            FixtureDesk desk=new FixtureDesk { ClipboardRevision=7,Sample="pre-existing test content" };
            Rectangle work=Screen.PrimaryScreen.WorkingArea;
            float s=(float)Win.ScaleAt(new Point(work.Left,work.Top));
            Point center=new Point((int)(work.Left+work.Width*.66),work.Bottom);
            int delivered=0;
            using(Overlay probe=new Overlay("Taskbar Tails - pass-through fixture",false))
            {
                probe.Paint(new Rectangle(center.X-(int)(200*s),center.Y-(int)(250*s),(int)(400*s),(int)(256*s)),
                    delegate(Graphics g){g.Clear(Color.FromArgb(4,Ink.Forest));},255);
                probe.Handler=delegate(ref Message m){if(m.Msg==Win.LBUTTONDOWN){delivered++;return true;}return false;};
                IntPtr testedHandle;
                using(Companion app=new Companion(desk,false))
                {
                    testedHandle=app.View.Pet.Handle;
                    Application.DoEvents();
                    Check(app.Tray.Installed,"Notification-area icon accepted by the Windows shell");
                    Check(app.ListenerInstalled,"Windows clipboard-format listener registered");
                    int style=Win.GetWindowLong(app.View.Pet.Handle,-20);
                    Check((style&(Win.TOPMOST|Win.NOACTIVATE|Win.LAYERED|Win.TOOLWINDOW))==(Win.TOPMOST|Win.NOACTIVATE|Win.LAYERED|Win.TOOLWINDOW),"Pet HWND has topmost, layered, tool-window and no-activate styles");
                    Check(Win.GetForegroundWindow()==foreground && Win.SendMessage(app.View.Pet.Handle,Win.MOUSEACTIVATE,IntPtr.Zero,IntPtr.Zero).ToInt32()==3,"Launch and native mouse activation preserve foreground focus");
                    Rectangle b=app.View.Pet.Bounds;
                    Point outside=new Point(b.Left+2,b.Top+3);
                    IntPtr receiver=Win.WindowFromPoint(new Win.Pt(outside));
                    Check(receiver==probe.Handle,"Windows routes the transparent sprite margin to the underlying HWND");
                    Win.SendMessage(receiver,Win.LBUTTONDOWN,IntPtr.Zero,IntPtr.Zero);
                    Check(delivered==1,"Underlying fixture receives the adjacent click");
                    Rectangle bubble=app.View.Speech.Bounds;
                    Point speech=new Point(bubble.Left+bubble.Width/2,bubble.Top+(int)(12*s));
                    Check(Win.WindowFromPoint(new Win.Pt(speech))==probe.Handle,"Even painted speech-bubble pixels are click-through");
                    Point solid=new Point(b.X+b.Width/2,b.Bottom-(int)(50*s));
                    Check(app.View.Pet.AlphaAt(solid.X-b.X,solid.Y-b.Y)>128 && Win.WindowFromPoint(new Win.Pt(solid))==app.View.Pet.Handle,"An opaque body pixel receives pet input");
                    Win.SendMessage(app.View.Pet.Handle,Win.CLIPBOARDUPDATE,IntPtr.Zero,IntPtr.Zero);desk.Step(app,.03);
                    Check(desk.Reads==0,"Pre-launch clipboard revision is ignored");
                    desk.LeftDown=true;desk.Pointer=solid;
                    PetMessage(app,Win.LBUTTONDOWN,solid);
                    Check(app.Pip.State==Mood.Held && Win.GetCapture()==app.View.Pet.Handle,"Direct grab enters the lifted pose and captures the pointer");
                    double initialY=app.Pip.Y;
                    for(int i=0;i<4;i++)
                    {
                        desk.Pointer=new Point(desk.Pointer.X+(int)(12*s),desk.Pointer.Y-(int)(24*s));
                        desk.Step(app,.021);
                        PetMessage(app,Win.MOUSEMOVE,desk.Pointer);
                    }
                    Check(app.Pip.Y<initialY-70*s,"Grabbed pet follows the test pointer");
                    desk.LeftDown=false;desk.Now+=.011;PetMessage(app,Win.LBUTTONUP,desk.Pointer);
                    Check(app.Pip.State==Mood.Flight && app.Pip.Vx>150*s && app.Pip.Vy< -300*s && Win.GetCapture()!=app.View.Pet.Handle,"Release transfers measured hand velocity and releases capture");
                    bool bounced=false;double releaseY=app.Pip.Y;desk.Step(app,.032);
                    Check(app.Pip.Y<releaseY,"Upward toss rises before falling");
                    for(int i=0;i<240 && app.Pip.State==Mood.Flight;i++)
                    {
                        double vy=app.Pip.Vy;desk.Step(app,.016);
                        if(vy>0 && app.Pip.Vy<0) bounced=true;
                    }
                    Check(bounced && app.Pip.State==Mood.Patrol && app.Pip.Y==work.Bottom,"Gravity, damped bounce, landing and resumed taskbar walk");
                    desk.IdleMs=59999;desk.Step(app,.032);Check(app.Pip.State==Mood.Patrol,"Still awake at 59,999 ms of global inactivity");
                    desk.IdleMs=60000;desk.Step(app,.032);Check(app.Pip.State==Mood.Nap && !app.Pip.ManualNap,"Automatic sleep at 60,000 ms of global inactivity");
                    desk.IdleMs=0;desk.Step(app,.032);Check(app.Pip.State==Mood.Patrol,"Next desktop input wakes an automatic nap");
                    TrayMenu(app,desk);Click(app.Menu,Command.Bed);desk.Step(app,.032);
                    Check(app.Pip.State==Mood.Nap && app.Pip.ManualNap && !app.Menu.Visible,"Tray sleep command latches through unrelated input");
                    TrayMenu(app,desk);Click(app.Menu,Command.Bed);Check(app.Pip.State==Mood.Patrol,"Tray wake command");
                    TrayMenu(app,desk);Click(app.Menu,Command.Feed);
                    Check(app.Pip.State==Mood.Biscuit && app.Pip.Sprite=="biscuit","Tray feeding shows the biscuit pose");
                    for(int i=0;i<48;i++)desk.Step(app,.1);
                    Check(app.Pip.State==Mood.Patrol,"Feeding completes and patrol resumes");
                    Mood[] expected={Mood.Scout,Mood.Count,Mood.Debug,Mood.Read};
                    for(int i=0;i<4;i++)
                    {
                        desk.Copy(app,Finds.Samples[i]);
                        Check(app.Pip.State==expected[i] && app.Pip.LineUntil>app.Pip.Time,
                            "Clipboard update produces "+app.Pip.Activity.ToLowerInvariant()+" with its own line and pose");
                    }
                    desk.BusyReads=1;desk.Copy(app,"42");
                    Check(app.Pip.State==Mood.Read,"Clipboard contention leaves the current reaction intact");
                    desk.Step(app,.1);Check(app.Pip.State==Mood.Count,"Clipboard retry succeeds after the writer releases it");
                    int reads=desk.Reads;
                    Win.SendMessage(app.View.Pet.Handle,Win.CLIPBOARDUPDATE,IntPtr.Zero,IntPtr.Zero);desk.Step(app,.032);
                    Check(desk.Reads==reads,"Repeated notifications for one clipboard revision are ignored");
                    TrayMenu(app,desk);Click(app.Menu,Command.Guide);Application.DoEvents();
                    Check(app.Guide.Visible && !app.Menu.Visible,"Field guide opens from the tray");
                    foreach(Command c in new[]{Command.Link,Command.Number,Command.Code,Command.Story})
                    {
                        Click(app.Guide,c);
                        Check(app.Pip.State==expected[(int)c-(int)Command.Link],"Field-guide sample button "+c);
                    }
                    Click(app.Guide,Command.Feed);Check(app.Pip.State==Mood.Biscuit,"Field-guide biscuit button");
                    Click(app.Guide,Command.Bed);Check(app.Pip.ManualNap,"Field-guide sleep button");
                    Click(app.Guide,Command.Bed);Check(app.Pip.State==Mood.Patrol,"Field-guide wake button");
                    Check(Win.GetForegroundWindow()==foreground,"Drag, toss, reactions, tray and open field guide preserve foreground focus");
                    if(plate!=null)app.Guide.SavePlate(plate);
                    Click(app.Guide,Command.Close);Check(!app.Guide.Visible,"Field guide close returns to the desktop pet");
                    PetMessage(app,Win.RBUTTONUP,new Point((int)app.Pip.X,(int)app.Pip.Y-20));
                    Check(app.Menu.Visible,"Right-click on Pip opens the same care menu");
                    desk.Pointer=work.Location;desk.LeftDown=true;desk.Step(app,.032);desk.LeftDown=false;
                    Check(!app.Menu.Visible,"Click outside dismisses the care menu");
                    TrayMenu(app,desk);Click(app.Menu,Command.Exit);Check(app.QuitRequested,"Tray exit ends the application context");
                }
                Check(!Win.IsWindow(testedHandle),"Exit destroys the pet HWND");
            }
        }
        public static int Run(string[] args)
        {
            string report=args.Length>1?args[1]:null, plate=args.Length>2?args[2]:null;
            int status=0;
            try { Classifier();Model();NativeChecks(plate); }
            catch(Exception e) { status=1;log.Add("FAIL  "+e.ToString()); }
            log.Add(status==0?"All checks passed.":"Verification failed.");
            if(report!=null) File.WriteAllLines(report,log);
            Console.WriteLine(String.Join(Environment.NewLine,log));
            return status;
        }
    }
}
