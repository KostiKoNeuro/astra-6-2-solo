using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TaskbarTails
{
    internal enum Command { Feed, Bed, Guide, Exit, Close, Link, Number, Code, Story }

    // The keeper's controls are deliberately mouse-operable without activation.
    // Action regions are also exposed as named buttons to Windows accessibility.
    internal abstract class PaperWindow : Form
    {
        internal class ActionArea
        {
            public RectangleF Bounds;
            public Command Command;
            public Func<string> Name;
        }
        protected readonly Pip Pet;
        protected readonly Art Art;
        protected readonly List<ActionArea> Areas = new List<ActionArea>();
        readonly Action<Command> execute;
        protected readonly Size Page;
        float zoom = 1;
        int hover = -1, down = -1;
        bool moving;
        Point grip;
        public float Zoom { get { return zoom; } }
        protected PaperWindow(Pip pet, Art art, Action<Command> execute, Size size, string title)
        {
            Pet = pet; Art = art; this.execute = execute; Page = size;
            Text = title; AccessibleName = title;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.Selectable,false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint,true);
            BackColor = Ink.Paper;
            Size = size;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp=base.CreateParams; cp.ExStyle |= Win.NOACTIVATE|Win.TOOLWINDOW; cp.ClassStyle |= 0x20000; return cp; }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int rounded = 2;
            Win.DwmSetWindowAttribute(Handle,33,ref rounded,4);
        }
        protected void Button(Command c, RectangleF r, Func<string> name)
        {
            Areas.Add(new ActionArea {Command=c,Bounds=r,Name=name});
        }
        protected void Button(Command c, RectangleF r, string name) { Button(c,r,delegate{return name;}); }
        protected bool Hot(Command c) { return hover >= 0 && Areas[hover].Command==c; }
        protected bool Pressed(Command c) { return down >= 0 && Areas[down].Command==c; }
        internal Point ButtonCenter(Command c)
        {
            ActionArea a=Areas.Find(delegate(ActionArea b){return b.Command==c;});
            return new Point((int)((a.Bounds.X+a.Bounds.Width/2)*zoom),(int)((a.Bounds.Y+a.Bounds.Height/2)*zoom));
        }
        int Hit(Point p)
        {
            for (int i=0;i<Areas.Count;i++) if (Areas[i].Bounds.Contains(p.X/zoom,p.Y/zoom)) return i;
            return -1;
        }
        protected abstract void DrawPage(Graphics g);
        protected override void OnPaint(PaintEventArgs e)
        {
            Ink.Prepare(e.Graphics); e.Graphics.ScaleTransform(zoom,zoom); DrawPage(e.Graphics);
        }
        internal void SavePlate(string path)
        {
            using (Bitmap image=new Bitmap(Page.Width*2,Page.Height*2))
            using (Graphics g=Graphics.FromImage(image))
            {
                Ink.Prepare(g); g.ScaleTransform(2,2); DrawPage(g); image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        public void Present(Point anchor, bool center)
        {
            Rectangle work=Screen.FromPoint(anchor).WorkingArea;
            zoom=(float)Math.Min(Win.ScaleAt(anchor),Math.Min((work.Width-12.0)/Page.Width,(work.Height-12.0)/Page.Height));
            int w=(int)Math.Round(Page.Width*zoom), h=(int)Math.Round(Page.Height*zoom);
            int x=center?work.Left+(work.Width-w)/2:anchor.X-w+16;
            int y=center?work.Top+(work.Height-h)/2:anchor.Y-h-8;
            Bounds=new Rectangle((int)Pip.Clamp(x,work.Left+4,work.Right-w-4),
                (int)Pip.Clamp(y,work.Top+4,work.Bottom-h-4),w,h);
            hover=down=-1;
            if (!Visible) Show();
            Win.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);
            Invalidate();
        }
        protected virtual bool DragHeader { get { return false; } }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg==Win.MOUSEACTIVATE) { m.Result=new IntPtr(3);return; }
            if (m.Msg==Win.CLOSE) { Hide(); return; }
            if (m.Msg==Win.MOUSEMOVE || m.Msg==Win.LBUTTONDOWN || m.Msg==Win.LBUTTONUP)
            {
                Point p=Win.MessagePoint(m.LParam);
                int hit=Hit(p);
                if (m.Msg==Win.MOUSEMOVE)
                {
                    if ((moving || down>=0) && (m.WParam.ToInt64()&1)==0)
                    {
                        moving=false;down=-1;
                        if (Win.GetCapture()==Handle) Win.ReleaseCapture();
                    }
                    if (moving) { Point screen=PointToScreen(p); Location=new Point(screen.X-grip.X,screen.Y-grip.Y); }
                    else if (hover!=hit)
                    {
                        hover=hit; Cursor=hit>=0?Cursors.Hand:Cursors.Default; Invalidate();
                    }
                    if (!moving && down < 0) base.WndProc(ref m);
                }
                else if (m.Msg==Win.LBUTTONDOWN)
                {
                    down=hit;
                    moving=DragHeader && hit<0 && p.Y<54*zoom;
                    grip=p;
                    if (hit>=0 || moving) Win.SetCapture(Handle);
                    Invalidate();
                }
                else
                {
                    int action=down;
                    moving=false; down=-1;
                    if (Win.GetCapture()==Handle) Win.ReleaseCapture();
                    Invalidate();
                    if (action>=0 && action==hit) InvokeAction(Areas[hit].Command);
                }
                return;
            }
            if (m.Msg==0x215) { moving=false; down=-1; }
            base.WndProc(ref m);
        }
        protected override void OnMouseLeave(EventArgs e) { hover=-1;Invalidate();base.OnMouseLeave(e); }
        void InvokeAction(Command c)
        {
            if (this is CareMenu || c==Command.Close) Hide();
            if (c!=Command.Close) execute(c);
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new PageAccess(this); }
        sealed class PageAccess : ControlAccessibleObject
        {
            readonly PaperWindow page;
            public PageAccess(PaperWindow p):base(p){page=p;}
            public override int GetChildCount(){return page.Areas.Count;}
            public override AccessibleObject GetChild(int index)
            {
                return index>=0 && index<page.Areas.Count ? new ButtonAccess(page,index,this) : null;
            }
        }
        sealed class ButtonAccess : AccessibleObject
        {
            readonly PaperWindow page;
            readonly int index;
            readonly AccessibleObject parent;
            public ButtonAccess(PaperWindow p,int i,AccessibleObject parent){page=p;index=i;this.parent=parent;}
            public override AccessibleObject Parent {get{return parent;}}
            public override string Name {get{return page.Areas[index].Name();} set{}}
            public override AccessibleRole Role {get{return AccessibleRole.PushButton;}}
            public override string DefaultAction {get{return "Press";}}
            public override Rectangle Bounds
            {
                get
                {
                    RectangleF b=page.Areas[index].Bounds;float s=page.Zoom;
                    return page.RectangleToScreen(Rectangle.Round(new RectangleF(b.X*s,b.Y*s,b.Width*s,b.Height*s)));
                }
            }
            public override void DoDefaultAction(){page.BeginInvoke(new Action(delegate{page.InvokeAction(page.Areas[index].Command);}));}
        }
    }

    internal sealed class FieldGuide : PaperWindow
    {
        const int W=860, H=564;
        static readonly string[] names={"Link","Number","Code","Long text"};
        static readonly string[] samples={"https://...","1,024","{ ... }","Once upon..."};
        static readonly string[] keys={"scout","count","debug","read"};
        static readonly Color[] washes={Color.FromArgb(231,236,218),Color.FromArgb(246,232,207),
            Color.FromArgb(224,236,228),Color.FromArgb(241,227,215)};
        static readonly Command[] commands={Command.Link,Command.Number,Command.Code,Command.Story};
        public FieldGuide(Pip p,Art a,Action<Command> action):base(p,a,action,new Size(W,H),"Taskbar Tails - Companion field guide")
        {
            Button(Command.Close,new RectangleF(810,0,50,55),"Close field guide");
            Button(Command.Feed,new RectangleF(454,279,181,43),"Feed a biscuit");
            Button(Command.Bed,new RectangleF(644,279,188,43),delegate{return Pet.State==Mood.Nap?"Wake Pip":"Put to sleep";});
            for (int i=0;i<4;i++) Button(commands[i],new RectangleF(28+i*204,405,193,101),"Try the "+names[i].ToLowerInvariant()+" sample");
        }
        protected override bool DragHeader {get{return true;}}
        protected override void DrawPage(Graphics g)
        {
            g.Clear(Ink.Paper);
            using (Brush b=new SolidBrush(Ink.Forest)) g.FillRectangle(b,0,0,W,55);
            Ink.Box(g,22,11,33,33,9,Ink.Honey,Color.Transparent);
            Ink.Glyph(g,"paw",27,16,23,Ink.Forest);
            Ink.Text(g,"Taskbar Tails",66,3,184,49,23,Ink.Cream,"Georgia",FontStyle.Bold);
            Ink.Rule(g,Color.FromArgb(104,120,96),245,18,245,37);
            Ink.Text(g,"Companion field guide",260,4,230,47,13,Color.FromArgb(218,226,204));
            if (Hot(Command.Close)) Ink.Box(g,816,10,33,34,7,Color.FromArgb(73,94,73),Color.Transparent);
            Ink.Glyph(g,"close",824,18,18,Ink.Cream);
            DrawHabitat(g);
            Ink.Glyph(g,"leaf",455,85,17,Ink.Rust);
            Ink.Text(g,"Procyon lotor",480,80,270,28,15,Ink.Rust,"Georgia",FontStyle.Italic);
            Ink.Text(g,"Meet Pip.",451,112,378,65,51,Ink.Forest,"Georgia",FontStyle.Bold);
            Ink.Text(g,"I picked a raccoon because I like to turn a puzzling find over in both paws until it makes sense.",
                456,183,350,76,17,Ink.Dark,"Segoe UI",FontStyle.Regular,StringAlignment.Near,true);
            CareButton(g,Command.Feed,454,181,"biscuit","Feed a biscuit",true);
            bool nap=Pet.State==Mood.Nap;
            CareButton(g,Command.Bed,644,188,nap?"sun":"moon",nap?"Wake Pip":"Put to sleep",false);
            Ink.Rule(g,Ink.Line,28,346,832,346);
            Ink.Text(g,"Clipboard curiosities",27,358,405,39,28,Ink.Forest,"Georgia",FontStyle.Bold);
            Ink.Text(g,"Pick a sample to try it",521,367,310,26,13,Ink.Muted,"Segoe UI",FontStyle.Regular,StringAlignment.Far);
            for (int i=0;i<4;i++) DrawSpecimen(g,i);
            using (Brush b=new SolidBrush(Color.FromArgb(237,239,225))) g.FillRectangle(b,0,529,W,35);
            Ink.Rule(g,Ink.Line,0,529,W,529);
            Ink.Glyph(g,"mouse",29,538,16,Ink.Muted);
            Ink.Text(g,"Grab and toss Pip",51,531,172,31,12,Ink.Muted);
            Ink.Glyph(g,"clock",213,538,16,Ink.Muted);
            Ink.Text(g,"Naps after one quiet minute",237,531,257,31,12,Ink.Muted);
            Ink.Glyph(g,"leaf",728,539,14,Ink.Muted);
            Ink.Text(g,"Lives offline",750,531,84,31,12,Ink.Muted);
        }
        void DrawHabitat(Graphics g)
        {
            RectangleF r=new RectangleF(28,80,400,243);
            GraphicsState save=g.Save();
            using (GraphicsPath clip=Ink.Round(r.X,r.Y,r.Width,r.Height,12)) g.SetClip(clip);
            g.DrawImage(Art.Clearing,r);
            float center=230+(float)Math.Sin(Pet.Gait*.21)*35;
            float baseY=304;
            using (Brush b=new SolidBrush(Color.FromArgb(35,66,72,42))) g.FillEllipse(b,center-55,baseY-7,112,12);
            Art.DrawPip(g,Pet,center,baseY,1.54f,true);
            using (Font f=new Font("Segoe UI",12,FontStyle.Bold,GraphicsUnit.Pixel))
            {
                float badgeW=g.MeasureString(Pet.Activity,f).Width+27;
                Ink.Box(g,42,94,badgeW,25,12,Color.FromArgb(245,Ink.Cream),Color.FromArgb(100,Ink.Muted));
                using (Brush b=new SolidBrush(Pet.State==Mood.Nap?Ink.Rust:Color.FromArgb(78,112,67))) g.FillEllipse(b,53,104,6,6);
                Ink.Text(g,Pet.Activity,65,93,badgeW-25,26,12,Ink.Forest,"Segoe UI",FontStyle.Bold);
            }
            if (Pet.State!=Mood.Patrol && Pet.State!=Mood.Nap && Pet.LineUntil>Pet.Time && !String.IsNullOrEmpty(Pet.Line))
            {
                Ink.Box(g,119,130,286,27,12,Ink.Cream,Color.FromArgb(120,Ink.Muted));
                Ink.Text(g,Pet.Line,122,128,281,29,13,Ink.Forest,"Georgia",FontStyle.Italic,StringAlignment.Center);
            }
            if (Pet.State==Mood.Nap)
                Ink.Text(g,"z z z",center+33,182+(float)Math.Sin(Pet.Age*2)*3,68,32,22,Ink.Forest,"Georgia",FontStyle.Italic);
            g.Restore(save);
            using (GraphicsPath path=Ink.Round(r.X,r.Y,r.Width,r.Height,12))
            using (Pen p=new Pen(Color.FromArgb(150,Ink.Muted))) g.DrawPath(p,path);
        }
        void CareButton(Graphics g,Command c,int x,int w,string icon,string label,bool primary)
        {
            Color fill=primary?Ink.Forest:Ink.Paper;
            if (Hot(c)) fill=primary?Color.FromArgb(56,85,61):Ink.Sage;
            if (Pressed(c)) fill=primary?Ink.Dark:Ink.Line;
            Ink.Box(g,x,279,w,43,8,fill,primary?fill:Color.FromArgb(170,180,157));
            Color text=primary?Ink.Cream:Ink.Forest;
            Ink.Glyph(g,icon,x+18,291,19,primary?Ink.Honey:text);
            Ink.Text(g,label,x+49,278,w-54,44,15,text,"Segoe UI",FontStyle.Bold);
        }
        void DrawSpecimen(Graphics g,int i)
        {
            int x=28+i*204;
            Command c=commands[i]; bool hot=Hot(c), live=Pet.Sprite==keys[i];
            Ink.Box(g,x,405,193,101,9,washes[i],hot||live?Ink.Forest:Ink.Line);
            Ink.Text(g,names[i],x+13,423,101,30,20,Ink.Forest,"Georgia",FontStyle.Bold);
            Ink.Text(g,samples[i],x+14,456,103,21,12,Ink.Muted,"Consolas");
            Bitmap pic=Art[keys[i]];
            float s=Math.Min(91f/pic.Width,78f/pic.Height)*(hot?1.035f:1f);
            g.DrawImage(pic,x+186-pic.Width*s,499-pic.Height*s,pic.Width*s,pic.Height*s);
            Ink.Glyph(g,"arrow",x+169,413,14,hot?Ink.Forest:Ink.Muted);
        }
    }

    internal sealed class CareMenu : PaperWindow
    {
        static readonly Command[] actions={Command.Feed,Command.Bed,Command.Guide,Command.Exit};
        public CareMenu(Pip p,Art a,Action<Command> action):base(p,a,action,new Size(252,258),"Pip - Care menu")
        {
            for(int i=0;i<4;i++)
            {
                int n=i;
                Button(actions[i],new RectangleF(7,83+i*40+(i==3?5:0),238,39),delegate{return Label(n);});
            }
        }
        string Label(int i)
        {
            return i==0?"Feed a biscuit":i==1?(Pet.State==Mood.Nap?"Wake Pip":"Put to sleep"):i==2?"Field guide":"Exit Taskbar Tails";
        }
        protected override void DrawPage(Graphics g)
        {
            g.Clear(Ink.Paper);
            using (Brush b=new SolidBrush(Ink.Forest)) g.FillRectangle(b,0,0,252,77);
            Ink.Box(g,13,10,56,56,12,Ink.Sage,Color.Transparent);
            g.DrawImage(Art["step"],17,14,47,48);
            Ink.Text(g,"Pip",82,8,156,36,28,Ink.Cream,"Georgia",FontStyle.Bold);
            using (Brush b=new SolidBrush(Ink.Honey)) g.FillEllipse(b,84,54,5,5);
            Ink.Text(g,Pet.Activity,95,42,146,26,13,Color.FromArgb(222,231,207));
            for(int i=0;i<4;i++)
            {
                int y=83+i*40+(i==3?5:0);
                if(i==3) Ink.Rule(g,Ink.Line,17,y-4,235,y-4);
                if(Hot(actions[i])) Ink.Box(g,7,y,238,38,7,Ink.Sage,Color.Transparent);
                string glyph=i==0?"biscuit":i==1?(Pet.State==Mood.Nap?"sun":"moon"):i==2?"book":"exit";
                Ink.Glyph(g,glyph,20,y+9,20,i==0?Ink.Rust:Ink.Forest);
                Ink.Text(g,Label(i),54,y-1,186,39,15,Ink.Dark,"Segoe UI",i==0?FontStyle.Bold:FontStyle.Regular);
            }
        }
    }
}
