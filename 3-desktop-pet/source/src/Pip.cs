using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;

namespace TaskbarTails
{
    internal enum Mood { Patrol, Held, Flight, Nap, Biscuit, Scout, Count, Debug, Read, Petted }
    internal enum FindKind { None, Link, Number, Code, LongText }

    internal static class Finds
    {
        public static readonly string[] Samples = {
            "https://example.org/woodland", "-1,024.5", "if (crumbs > 0) { stash(crumbs); }",
            "Every evening, a little raccoon walked the entire length of the old stone wall. " +
            "He counted mossy stones, inspected beetles, and always found his way home in time for a biscuit."
        };
        static readonly Regex numeric = new Regex(
            @"^[+-]?(?:(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?%?$|^0[xX][0-9a-fA-F]+$",
            RegexOptions.CultureInvariant);
        static readonly Regex code = new Regex(
            @"\x60{3}|(?:^|\n)\s*(?:" +
            @"(?:public|private|protected|static|async|class|struct|interface|enum|def|function|import|using|package|namespace|#include|from)\s+[\w.<""']+.*[;:{(>]|" +
            @"(?:const|let|var|int|string|bool|double|float|char)\s+\w+\s*=|" +
            @"(?:if|while|for|switch|catch)\s*\(|(?:def|class)\s+\w+.*:|(?:import|from)\s+[\w.]+(?:\s+import\s+\w+)?\s*(?:$|\n)|" +
            @"(?:return|throw)\s+.+;|(?:SELECT|INSERT|UPDATE|DELETE)\b.*\b(?:FROM|INTO|SET)\b|" +
            @"[\w.]+\s*\([^)\n]*\)\s*;?\s*(?:$|\n)|(?:<[\w-]+(?:\s[^>]*)?>).*</[\w-]+>|\{\s*""[^""]+""\s*:)|" +
            @"=>\s*(?:\{|[\w.(])|\b\w+\s*=\s*[^=\n]+(?:;|$)|[{}]\s*(?:\n|;|$).*[;:]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        public static FindKind Classify(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return FindKind.None;
            string s = value.Trim();
            Uri uri;
            string address = s.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + s : s;
            if (s.Length < 8192 && s.IndexOfAny(new char[] {' ', '\t', '\r', '\n'}) < 0 &&
                Uri.TryCreate(address, UriKind.Absolute, out uri) && !String.IsNullOrEmpty(uri.Host) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return FindKind.Link;
            if (s.Length < 256 && numeric.IsMatch(s)) return FindKind.Number;
            if (code.IsMatch(s)) return FindKind.Code;
            return s.Length >= 160 ? FindKind.LongText : FindKind.None;
        }
    }

    internal sealed class Pip
    {
        public Mood State { get; private set; }
        public bool FacesRight { get; private set; }
        public bool ManualNap { get; private set; }
        public double X, Y, Vx, Vy;
        public double Scale { get; private set; }
        public Rectangle Meadow { get; private set; }
        public double Age { get; private set; }
        public double Time { get; private set; }
        public double Gait { get; private set; }
        public string Line { get; private set; }
        public double LineUntil { get; private set; }
        public double LandedAt { get; private set; }
        private double stateUntil;
        private bool dragged;
        private Point gripStart;
        private double gripX, gripY;
        private FindKind pending;
        struct Sample
        {
            public double Time;
            public Point Point;
            public Sample(double t, Point p) { Time = t; Point = p; }
        }
        readonly List<Sample> trail = new List<Sample>();
        public Pip(Rectangle work, double scale)
        {
            Meadow = work;
            Scale = scale;
            X = work.Left + work.Width * .66;
            Y = Floor;
            FacesRight = true;
            LandedAt = -10;
            Say("Grab me! Right-click for care.", 8);
        }
        public static double Clamp(double n, double lo, double hi) { return Math.Max(lo, Math.Min(n, hi)); }
        public double Floor { get { return Meadow.Bottom; } }
        double MinX { get { return Meadow.Left + 58 * Scale; } }
        double MaxX { get { return Math.Max(MinX, Meadow.Right - 58 * Scale); } }
        public void Terrain(Rectangle work, double scale)
        {
            Meadow = work;
            Scale = scale;
            if (State == Mood.Held) return;
            X = Clamp(X, MinX, MaxX);
            Y = State == Mood.Flight ? Clamp(Y, Meadow.Top + 112 * Scale, Floor) : Floor;
        }
        void Enter(Mood mood, double seconds)
        {
            State = mood;
            Age = 0;
            stateUntil = Time + seconds;
            ManualNap = false;
        }
        void Say(string text, double seconds) { Line = text; LineUntil = Time + seconds; }
        void Walk()
        {
            Enter(Mood.Patrol, 0);
            Vx = Vy = 0;
            Y = Floor;
        }
        public void Feed()
        {
            pending = FindKind.None;
            Y = Floor;
            Enter(Mood.Biscuit, 4.6);
            Say("A biscuit? I accept the bribe.", 3.9);
        }
        public void Sleep(bool manual)
        {
            pending = FindKind.None;
            Y = Floor;
            Enter(Mood.Nap, 0);
            ManualNap = manual;
            Say("", 0);
        }
        public void Wake()
        {
            Walk();
            Say("I was inspecting my eyelids.", 3.4);
        }
        public void Find(FindKind kind)
        {
            if (kind == FindKind.None) return;
            if (State == Mood.Held || State == Mood.Flight) { pending = kind; return; }
            switch (kind)
            {
                case FindKind.Link: Enter(Mood.Scout, 4.8); Say("A trail! I'll take point.", 4.2); break;
                case FindKind.Number: Enter(Mood.Count, 4.8); Say("Hang on. I've run out of toes.", 4.2); break;
                case FindKind.Code: Enter(Mood.Debug, 4.8); Say("Found the bug. It has legs.", 4.2); break;
                case FindKind.LongText: Enter(Mood.Read, 5.2); Say("I'll need a bigger bookmark.", 4.6); break;
            }
        }
        public void Grab(Point pointer, double now)
        {
            gripStart = pointer;
            gripX = X - pointer.X;
            gripY = Y - pointer.Y;
            dragged = false;
            Vx = Vy = 0;
            trail.Clear();
            trail.Add(new Sample(now, pointer));
            Enter(Mood.Held, 0);
            Say("I appear to be luggage.", 2.2);
        }
        public void Drag(Point pointer, double now)
        {
            if (State != Mood.Held) return;
            if (Math.Abs(pointer.X - gripStart.X) + Math.Abs(pointer.Y - gripStart.Y) > 5 * Scale) dragged = true;
            X = Clamp(pointer.X + gripX, MinX, MaxX);
            Y = Clamp(pointer.Y + gripY, Meadow.Top + 112 * Scale, Floor);
            while (trail.Count > 0 && now - trail[0].Time > .135) trail.RemoveAt(0);
            if (trail.Count == 0 || trail[trail.Count - 1].Time < now) trail.Add(new Sample(now, pointer));
            else trail[trail.Count - 1] = new Sample(now, pointer);
        }
        public void Release(Point pointer, double now)
        {
            if (State != Mood.Held) return;
            Drag(pointer, now);
            if (!dragged && Y >= Floor - 2 * Scale)
            {
                Enter(Mood.Petted, 1.8);
                Say("That was the good ear.", 1.8);
                CheckPocket();
                return;
            }
            Sample first = trail[0];
            double elapsed = Math.Max(.016, now - first.Time);
            Vx = Clamp(.88 * (pointer.X - first.Point.X) / elapsed, -1460 * Scale, 1460 * Scale);
            Vy = Clamp(.88 * (pointer.Y - first.Point.Y) / elapsed, -1500 * Scale, 1250 * Scale);
            if (Math.Abs(Vx) > 30 * Scale) FacesRight = Vx > 0;
            Enter(Mood.Flight, 0);
            Say(Math.Abs(Vx) + Math.Abs(Vy) > 500 * Scale ? "Unscheduled field trip!" : "", 1.15);
            trail.Clear();
        }
        void CheckPocket()
        {
            FindKind next = pending;
            pending = FindKind.None;
            Find(next);
        }
        public void Advance(double dt, uint idleMs)
        {
            Time += dt;
            Age += dt;
            switch (State)
            {
                case Mood.Held: return;
                case Mood.Flight:
                    Vy += 1720 * Scale * dt;
                    X += Vx * dt;
                    Y += Vy * dt;
                    if (X < MinX || X > MaxX)
                    {
                        X = Clamp(X, MinX, MaxX);
                        Vx *= -.62;
                        FacesRight = Vx > 0;
                    }
                    if (Y < Meadow.Top + 112 * Scale)
                    {
                        Y = Meadow.Top + 112 * Scale;
                        Vy = Math.Abs(Vy) * .48;
                    }
                    if (Y >= Floor && Vy >= 0)
                    {
                        Y = Floor;
                        LandedAt = Time;
                        if (Vy < 130 * Scale) { Walk(); CheckPocket(); }
                        else { Vy *= -.37; Vx *= .71; }
                    }
                    return;
                case Mood.Nap:
                    if (!ManualNap && idleMs < 60000) Wake();
                    return;
                case Mood.Patrol:
                    if (idleMs >= 60000) { Sleep(false); return; }
                    X += (FacesRight ? 37 : -37) * Scale * dt;
                    Gait += dt;
                    if (X <= MinX || X >= MaxX) { FacesRight = X <= MinX; X = Clamp(X, MinX, MaxX); }
                    return;
                default:
                    if (idleMs >= 60000) Sleep(false);
                    else if (Time >= stateUntil) Walk();
                    return;
            }
        }
        public string Sprite
        {
            get
            {
                switch (State)
                {
                    case Mood.Nap: return "nap";
                    case Mood.Flight: case Mood.Held: return "lift";
                    case Mood.Biscuit: return "biscuit";
                    case Mood.Scout: return "scout";
                    case Mood.Count: return "count";
                    case Mood.Debug: return "debug";
                    case Mood.Read: return "read";
                    default: return ((int)(Gait * 4.4) & 1) == 0 ? "stride" : "step";
                }
            }
        }
        public string Activity
        {
            get
            {
                switch (State)
                {
                    case Mood.Nap: return "Napping";
                    case Mood.Held: return "Hanging out";
                    case Mood.Flight: return "Airborne";
                    case Mood.Biscuit: return "Biscuit break";
                    case Mood.Scout: return "Scouting a link";
                    case Mood.Count: return "Counting toes";
                    case Mood.Debug: return "Bug hunting";
                    case Mood.Read: return "Deep in a story";
                    case Mood.Petted: return "Feeling loved";
                    default: return "On patrol";
                }
            }
        }
    }
}
