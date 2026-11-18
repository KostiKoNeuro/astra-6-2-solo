// An end-to-end timer run on a private desktop. Never open the session clipboard:
// the fixture owns every HGLOBAL and posts notifications only to its own pet HWND.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarTails
{
    internal static class ClipboardFixture
    {
        const string Report="docs/clipboard-verification.txt";
        static readonly List<string> lines=new List<string>();
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct Startup
        {
            public int Size;
            public string Reserved,Desktop,Title;
            public int X,Y,Width,Height,XChars,YChars,Fill,Flags;
            public short Show,Reserved2;
            public IntPtr ReservedPtr,Input,Output,Error;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Child {public IntPtr Process,Thread;public int ProcessId,ThreadId;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern bool CreateProcess(string exe,StringBuilder command,IntPtr procSec,IntPtr threadSec,bool inherit,
            int flags,IntPtr environment,string dir,ref Startup startup,out Child child);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint ms);
        [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr handle,out int code);
        [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr handle,int code);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint type,UIntPtr size);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr block);
        static void Require(bool ok,string check) { if(!ok)throw new InvalidOperationException(check); }
        static void Pass(string s) {lines.Add("PASS  "+s);}
        sealed class MemoryClipboard : IDesk, IDisposable
        {
            readonly Stopwatch clock=Stopwatch.StartNew();
            public readonly Stopwatch Inactivity=new Stopwatch();
            IntPtr block;
            double busyUntil;
            public int Reads,Retries;
            public double Now {get{return clock.Elapsed.TotalSeconds;}}
            public uint IdleMs {get{return (uint)Inactivity.ElapsedMilliseconds;}}
            public Point Pointer {get{return Point.Empty;}}
            public bool LeftDown {get{return false;}}
            public uint ClipboardRevision {get;private set;}
            public void Write(string sample)
            {
                Dispose();
                if(sample!=null)
                {
                    byte[] bytes=Encoding.Unicode.GetBytes(sample+"\0");
                    block=GlobalAlloc(2,new UIntPtr((uint)bytes.Length));
                    Require(block!=IntPtr.Zero,"Allocate native text block");
                    IntPtr p=Win.GlobalLock(block);Require(p!=IntPtr.Zero,"Lock test data");
                    try {Marshal.Copy(bytes,0,p,bytes.Length);} finally {Win.GlobalUnlock(block);}
                }
                ClipboardRevision++;
            }
            public void Copy(Companion app,string sample,double busySeconds)
            {
                Write(sample);busyUntil=Now+busySeconds;
                Win.PostMessage(app.View.Pet.Handle,Win.CLIPBOARDUPDATE,IntPtr.Zero,IntPtr.Zero);
            }
            public bool TryReadFind(out FindKind find)
            {
                Reads++;find=FindKind.None;
                if(Now<busyUntil){Retries++;return false;}
                string sample;
                if(!Desktop.ReadUnicodeBlock(block,out sample))return false;
                find=Finds.Classify(sample);
                return true;
            }
            public void Dispose(){if(block!=IntPtr.Zero){GlobalFree(block);block=IntPtr.Zero;}}
        }
        static void Until(Func<bool> condition,int limit,string failure)
        {
            Stopwatch clock=Stopwatch.StartNew();
            while(!condition())
            {
                Application.DoEvents();
                if(clock.ElapsedMilliseconds>limit)throw new TimeoutException(failure);
                Thread.Sleep(5);
            }
        }
        static int Launch()
        {
            string name="TaskbarTailsFixture-"+Guid.NewGuid().ToString("N");
            IntPtr desktop=CreateDesktop(name,IntPtr.Zero,IntPtr.Zero,0,0x000F01FFu,IntPtr.Zero);
            Require(desktop!=IntPtr.Zero,"Create private test desktop");
            try
            {
                string exe=Assembly.GetExecutingAssembly().Location;
                Startup start=new Startup {Size=Marshal.SizeOf(typeof(Startup)),Desktop=name};
                Child child;
                Require(CreateProcess(exe,new StringBuilder("\""+exe+"\" --worker"),IntPtr.Zero,IntPtr.Zero,false,
                    0x08000000,IntPtr.Zero,Environment.CurrentDirectory,ref start,out child),"Start worker on private desktop");
                try
                {
                    if(WaitForSingleObject(child.Process,95000)!=0)
                    {
                        TerminateProcess(child.Process,2);WaitForSingleObject(child.Process,4000);
                        throw new TimeoutException("Private desktop fixture exceeded its time limit.");
                    }
                    int code;Require(GetExitCodeProcess(child.Process,out code),"Read worker result");return code;
                }
                finally {CloseHandle(child.Thread);CloseHandle(child.Process);}
            }
            finally {CloseDesktop(desktop);}
        }
        static void Run()
        {
            Pass("Real-time app runs on a separate, undisplayed Windows desktop");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using(MemoryClipboard desk=new MemoryClipboard())
            {
                desk.Write("fixture seed");
                using(Companion app=new Companion(desk,true))
                {
                    Until(delegate{return app.Pip.Time>.13;},3000,"Run native animation timer");
                    Require(desk.Reads==0,"Startup skipped seeded text");
                    Pass("Native animation timer starts without reading existing text");
                    Mood[] expected={Mood.Scout,Mood.Count,Mood.Debug,Mood.Read};
                    for(int i=0;i<4;i++)
                    {
                        desk.Copy(app,Finds.Samples[i],0);
                        Mood target=expected[i];
                        Until(delegate{return app.Pip.State==target;},2500,"Clipboard notification for "+target);
                        Pass("Posted WM_CLIPBOARDUPDATE and native UTF-16 block produce "+app.Pip.Activity.ToLowerInvariant());
                    }
                    desk.Copy(app,"42\0ignored trailing allocation bytes",.2);
                    Until(delegate{return app.Pip.State==Mood.Count;},3000,"Retry native block after simulated contention");
                    Require(desk.Retries>=1,"At least one retry");
                    Pass("Busy fixture retries and the reader stops at the UTF-16 terminator");
                    int before=desk.Reads;
                    desk.Copy(app,null,0);
                    Until(delegate{return desk.Reads>before;},2500,"Non-text notification");
                    Require(app.Pip.State==Mood.Count,"Non-text copy leaves the current pose alone");
                    Pass("Absent text handle leaves Pip's current reaction alone");
                    desk.Copy(app,new string('k',30000),0);
                    Until(delegate{return app.Pip.State==Mood.Read;},3000,"Large native text sample");
                    Pass("Large native text allocation produces the reading pose");
                    desk.Inactivity.Restart();
                    Until(delegate{return desk.IdleMs>=59000;},61000,"Reach 59 seconds of fixture inactivity");
                    Require(app.Pip.State==Mood.Patrol,"Pet stays awake before the one-minute boundary");
                    Until(delegate{return app.Pip.State==Mood.Nap;},3500,"One-minute real-time nap");
                    Require(desk.IdleMs>=60000 && desk.IdleMs<62500,"Automatic nap occurred at the minute boundary");
                    Pass("Live timer auto-nap after "+desk.IdleMs+" ms of controlled inactivity");
                    desk.Inactivity.Restart();
                    Until(delegate{return app.Pip.State==Mood.Patrol;},1500,"Timed wake on new input");
                    Pass("Next input wakes Pip in the real message loop");
                    using(System.Windows.Forms.Timer quit=new System.Windows.Forms.Timer {Interval=60})
                    {
                        quit.Tick+=delegate
                        {
                            quit.Stop();
                            Win.SendMessage(app.View.Pet.Handle,Win.TRAY,new IntPtr(1),new IntPtr(Win.RBUTTONUP));
                            Point pt=app.Menu.ButtonCenter(Command.Exit);
                            IntPtr xy=new IntPtr((pt.X&0xffff)|((pt.Y&0xffff)<<16));
                            Win.SendMessage(app.Menu.Handle,Win.LBUTTONDOWN,new IntPtr(1),xy);
                            Win.SendMessage(app.Menu.Handle,Win.LBUTTONUP,IntPtr.Zero,xy);
                        };
                        quit.Start();
                        Application.Run(app);
                    }
                    Require(app.QuitRequested,"Tray exit stops the application message pump");
                    Pass("Tray exit returns from Application.Run and disposes the companion");
                }
            }
        }
        [STAThread]
        static int Main(string[] args)
        {
            int code=0;
            try
            {
                if(args.Length==0)return Launch();
                if(args[0]=="--overlay")
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    return Verification.Run(args);
                }
                Run();lines.Add("All real-time fixture checks passed.");
            }
            catch(Exception e){lines.Add("FAIL  "+e.Message);code=1;}
            File.WriteAllLines(Report,lines);
            return code;
        }
    }
}
