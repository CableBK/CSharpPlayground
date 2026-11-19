// ============================================================
//  CTC Railway Simulator (Centralized Traffic Control)
//  .NET 6+  |  dotnet new console  ->  แทนที่ Program.cs ด้วยไฟล์นี้
// ============================================================
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
new CtcSystem().Run();

enum Dir { East, West }

class Train
{
    public int Id;
    public Dir Dir;
    public Track At = null!;
}

class Track
{
    public string Name;
    public int Pos;                 // ตำแหน่งตามแนวราง (ใช้เรียงลำดับการเคลื่อนที่)
    public Train? Occupant;         // รถที่อยู่บนตอนรางนี้ (Track Circuit)
    public Route? LockedBy;         // เส้นทางที่ล็อกอยู่
    public bool Occupied => Occupant != null;
    public Track(string name, int pos) { Name = name; Pos = pos; }
}

class Switch
{
    public string Name;
    public bool Reverse;            // false = Normal (ตรง), true = Reverse (เข้าทางหลีก)
    public Track Zone;              // ตอนรางที่ประแจตั้งอยู่
    public Switch(string name, Track zone) { Name = name; Zone = zone; }
}

class Signal
{
    public string Name;
    public Dir Dir;
    public bool Green;
    public Signal(string name, Dir dir) { Name = name; Dir = dir; }
}

class Route
{
    public int Id;
    public string Name = "";
    public Dir Dir;
    public Signal Signal = null!;
    public List<Track> Tracks = new();
    public Dictionary<Switch, bool> Switches = new();   // ประแจ -> ตำแหน่งที่ต้องการ (Reverse?)
    public List<Track> Remaining = new();                // ตอนรางที่ยังล็อกอยู่
    public HashSet<Track> Visited = new();               // ตอนรางที่รถเคยเข้าแล้ว
    public bool Active;
    public bool Used;                                    // รถเข้าเขตเส้นทางแล้ว
}

class CtcSystem
{
    // ---------- โครงสร้างราง ----------
    readonly Track T1 = new("T1", 0), Z1 = new("Z1", 1), MAIN = new("MAIN", 2),
                   LOOP = new("LOOP", 2), Z2 = new("Z2", 3), T4 = new("T4", 4);
    readonly Switch SW1, SW2;
    readonly Signal S1 = new("S1", Dir.East), S2 = new("S2", Dir.West);
    readonly List<Route> routes = new();
    readonly List<Train> trains = new();
    readonly List<string> log = new();
    int time, nextTrainId = 1;

    public CtcSystem()
    {
        SW1 = new Switch("SW1", Z1);
        SW2 = new Switch("SW2", Z2);

        AddRoute(1, "ขาไปตะวันออก - ทางหลัก", Dir.East, S1, new[] { Z1, MAIN, Z2 }, false, false);
        AddRoute(2, "ขาไปตะวันออก - ทางหลีก", Dir.East, S1, new[] { Z1, LOOP, Z2 }, true, true);
        AddRoute(3, "ขาไปตะวันตก  - ทางหลัก", Dir.West, S2, new[] { Z2, MAIN, Z1 }, false, false);
        AddRoute(4, "ขาไปตะวันตก  - ทางหลีก", Dir.West, S2, new[] { Z2, LOOP, Z1 }, true, true);
    }

    void AddRoute(int id, string name, Dir dir, Signal sig, Track[] tracks, bool sw1Rev, bool sw2Rev)
    {
        var r = new Route { Id = id, Name = name, Dir = dir, Signal = sig };
        r.Tracks.AddRange(tracks);
        r.Switches[SW1] = sw1Rev;
        r.Switches[SW2] = sw2Rev;
        routes.Add(r);
    }

    void Log(string msg)
    {
        log.Add($"[{time:D3}] {msg}");
        if (log.Count > 8) log.RemoveAt(0);
    }

    // ---------- Main loop ----------
    public void Run()
    {
        Log("ระบบ CTC พร้อมใช้งาน พิมพ์ help เพื่อดูคำสั่ง");
        while (true)
        {
            Draw();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\nCTC> ");
            var line = Console.ReadLine();
            if (line == null) return;
            var p = line.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) { Tick(); continue; }

            switch (p[0])
            {
                case "q":
                case "quit":
                    return;

                case "r":
                case "route":
                    {
                        if (p.Length > 1 && int.TryParse(p[1], out int rid)) SetRoute(rid);
                        else Log("ใช้: route <1-4>");
                        break;
                    }
                case "c":
                case "cancel":
                    {
                        if (p.Length > 1 && int.TryParse(p[1], out int cid)) CancelRoute(cid);
                        else Log("ใช้: cancel <1-4>");
                        break;
                    }
                case "sw":
                    {
                        if (p.Length > 2 && int.TryParse(p[1], out int sid)) ThrowSwitch(sid, p[2]);
                        else Log("ใช้: sw <1-2> <n|r>");
                        break;
                    }
                case "t":
                case "train":
                    {
                        if (p.Length > 1 && (p[1] == "e" || p[1] == "w")) SpawnTrain(p[1] == "e" ? Dir.East : Dir.West);
                        else Log("ใช้: train <e|w>");
                        break;
                    }
                case "n":
                case "tick":
                    {
                        int k = 1;
                        if (p.Length > 1) int.TryParse(p[1], out k);
                        for (int i = 0; i < Math.Max(1, k); i++) Tick();
                        break;
                    }
                case "h":
                case "help":
                    PrintHelp();
                    break;
                default:
                    Log("ไม่รู้จักคำสั่ง (พิมพ์ help)");
                    break;
            }
        }
    }

    // ---------- Interlocking ----------
    void SetRoute(int id)
    {
        var r = routes.FirstOrDefault(x => x.Id == id);
        if (r == null) { Log($"ไม่มีเส้นทาง R{id}"); return; }
        if (r.Active) { Log($"R{id} ถูกตั้งไว้แล้ว"); return; }

        foreach (var t in r.Tracks)
        {
            if (t.Occupied) { Log($"ปฏิเสธ R{id}: ตอนราง {t.Name} มีรถอยู่"); return; }
            if (t.LockedBy != null) { Log($"ปฏิเสธ R{id}: ขัดแย้งกับ R{t.LockedBy.Id} ({t.Name})"); return; }
        }

        foreach (var kv in r.Switches) kv.Key.Reverse = kv.Value;   // สับประแจ
        foreach (var t in r.Tracks) t.LockedBy = r;                  // ล็อกเส้นทาง
        r.Remaining = new List<Track>(r.Tracks);
        r.Visited.Clear();
        r.Active = true;
        r.Used = false;
        r.Signal.Green = true;
        Log($"ตั้งเส้นทาง R{id} สำเร็จ สัญญาณ {r.Signal.Name} = เขียว");
    }

    void CancelRoute(int id)
    {
        var r = routes.FirstOrDefault(x => x.Id == id);
        if (r == null || !r.Active) { Log("ไม่มีเส้นทางนี้ที่ถูกตั้งอยู่"); return; }
        if (r.Used) { Log($"ยกเลิก R{id} ไม่ได้: รถเข้าเขตแล้ว"); return; }
        foreach (var t in r.Remaining) t.LockedBy = null;
        r.Remaining.Clear();
        r.Active = false;
        r.Signal.Green = false;
        Log($"ยกเลิกเส้นทาง R{id}");
    }

    void ThrowSwitch(int n, string pos)
    {
        var sw = n == 1 ? SW1 : n == 2 ? SW2 : null;
        if (sw == null) { Log("มีประแจ SW1, SW2 เท่านั้น"); return; }
        if (pos != "n" && pos != "r") { Log("ตำแหน่งต้องเป็น n หรือ r"); return; }
        if (sw.Zone.Occupied) { Log($"สับ {sw.Name} ไม่ได้: มีรถอยู่บนประแจ"); return; }
        if (sw.Zone.LockedBy != null) { Log($"สับ {sw.Name} ไม่ได้: ถูกล็อกโดย R{sw.Zone.LockedBy.Id}"); return; }
        sw.Reverse = pos == "r";
        Log($"{sw.Name} -> {(sw.Reverse ? "REVERSE" : "NORMAL")}");
    }

    // ---------- Simulation ----------
    void SpawnTrain(Dir dir)
    {
        var start = dir == Dir.East ? T1 : T4;
        if (start.Occupied) { Log($"{start.Name} มีรถอยู่แล้ว"); return; }
        var tr = new Train { Id = nextTrainId++, Dir = dir, At = start };
        start.Occupant = tr;
        trains.Add(tr);
        Log($"รถไฟ #{tr.Id} เข้าระบบที่ {start.Name} ({(dir == Dir.East ? "ไปตะวันออก" : "ไปตะวันตก")})");
    }

    Track? NextTrack(Train tr)
    {
        if (tr.Dir == Dir.East)
        {
            if (tr.At == T1) return Z1;
            if (tr.At == Z1) return SW1.Reverse ? LOOP : MAIN;
            if (tr.At == MAIN || tr.At == LOOP) return Z2;
            if (tr.At == Z2) return T4;
            return null;
        }
        else
        {
            if (tr.At == T4) return Z2;
            if (tr.At == Z2) return SW2.Reverse ? LOOP : MAIN;
            if (tr.At == MAIN || tr.At == LOOP) return Z1;
            if (tr.At == Z1) return T1;
            return null;
        }
    }

    void Tick()
    {
        time++;

        // เคลื่อนรถที่อยู่ข้างหน้าสุดก่อน เพื่อไม่ให้ขวางกันเอง
        var order = trains.Where(t => t.Dir == Dir.East).OrderByDescending(t => t.At.Pos)
            .Concat(trains.Where(t => t.Dir == Dir.West).OrderBy(t => t.At.Pos)).ToList();
        foreach (var tr in order) MoveTrain(tr);

        ReleaseRoutes();
    }

    void MoveTrain(Train tr)
    {
        var next = NextTrack(tr);
        if (next == null)
        {
            tr.At.Occupant = null;
            trains.Remove(tr);
            Log($"รถไฟ #{tr.Id} ออกจากระบบ");
            return;
        }
        if (next.Occupied) return;

        // ตรวจสัญญาณที่ปากทางเข้าสถานี
        bool entering = (tr.Dir == Dir.East && tr.At == T1 && next == Z1) ||
                        (tr.Dir == Dir.West && tr.At == T4 && next == Z2);
        if (entering)
        {
            var r = routes.FirstOrDefault(x => x.Dir == tr.Dir && x.Active && !x.Used && x.Signal.Green);
            if (r == null) return;                 // สัญญาณแดง รอ
            r.Used = true;
            r.Signal.Green = false;                // รถผ่านแล้ว สัญญาณกลับเป็นแดงอัตโนมัติ
            Log($"รถไฟ #{tr.Id} ผ่านสัญญาณ {r.Signal.Name} (R{r.Id})");
        }

        tr.At.Occupant = null;
        tr.At = next;
        next.Occupant = tr;
    }

    // ปลดล็อกตอนรางทีละตอน หลังรถผ่านพ้นไปแล้ว (Sequential Release)
    void ReleaseRoutes()
    {
        foreach (var r in routes.Where(x => x.Active && x.Used))
        {
            foreach (var t in r.Remaining.ToList())
            {
                if (t.Occupied) r.Visited.Add(t);
                else if (r.Visited.Contains(t))
                {
                    t.LockedBy = null;
                    r.Remaining.Remove(t);
                    Log($"ปลดล็อก {t.Name} (R{r.Id})");
                }
            }
            if (r.Remaining.Count == 0)
            {
                r.Active = false;
                Log($"เส้นทาง R{r.Id} เสร็จสิ้น");
            }
        }
    }

    // ---------- แสดงผล ----------
    int col;
    void Piece(string s, ConsoleColor c)
    {
        Console.ForegroundColor = c;
        Console.Write(s);
        col += s.Length;
    }

    ConsoleColor TrackColor(Track t) =>
        t.Occupied ? ConsoleColor.Red : t.LockedBy != null ? ConsoleColor.Green : ConsoleColor.DarkGray;

    void Seg(Track t)
    {
        char mark = t.Occupant == null ? ' ' : t.Occupant.Dir == Dir.East ? '>' : '<';
        Piece($"[{t.Name,-5}{mark}]", TrackColor(t));
    }

    void Sig(Signal s)
    {
        string arrow = s.Dir == Dir.East ? ">" : "<";
        Piece($"({s.Name}{arrow}{(s.Green ? "G" : "R")})", s.Green ? ConsoleColor.Green : ConsoleColor.Red);
    }

    void Draw()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================== CTC CONTROL PANEL ====================");
        Console.WriteLine($" เวลาจำลอง: {time}    รถในระบบ: {trains.Count}\n");

        var gray = ConsoleColor.Gray;
        col = 0;
        Piece(" W ", ConsoleColor.White);
        Piece("=", gray); Seg(T1);
        Piece("=", gray); Sig(S1);
        Piece("=", gray); Seg(Z1);
        Piece("=", gray);
        int mainCol = col;
        Seg(MAIN);
        Piece("=", gray); Seg(Z2);
        Piece("=", gray); Sig(S2);
        Piece("=", gray); Seg(T4);
        Piece("= E", ConsoleColor.White);
        Console.WriteLine();

        Console.Write(new string(' ', mainCol - 1));
        col = mainCol - 1;
        Piece("\\", gray); Seg(LOOP); Piece("/", gray);
        Console.WriteLine("\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($" ประแจ:  SW1 = {(SW1.Reverse ? "REVERSE" : "NORMAL ")}   SW2 = {(SW2.Reverse ? "REVERSE" : "NORMAL ")}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" สี: เทา=ว่าง  เขียว=ล็อกเส้นทาง  แดง=มีรถ\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(" เส้นทาง:");
        foreach (var r in routes)
        {
            Console.ForegroundColor = r.Active ? ConsoleColor.Green : ConsoleColor.Gray;
            string st = !r.Active ? "ว่าง" : r.Used ? "รถกำลังผ่าน" : "ตั้งแล้ว รอรถ";
            Console.WriteLine($"  R{r.Id}  {r.Name,-30} [{st}]");
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\n บันทึกเหตุการณ์:");
        Console.ForegroundColor = ConsoleColor.Gray;
        foreach (var l in log) Console.WriteLine("  " + l);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n (help = คำสั่ง | Enter = เดินเวลา 1 tick)");
    }

    void PrintHelp()
    {
        Log("route <1-4> ตั้งเส้นทาง | cancel <1-4> ยกเลิก");
        Log("sw <1|2> <n|r> สับประแจ | train <e|w> เพิ่มรถ");
        Log("tick [n] เดินเวลา | Enter = 1 tick | quit ออก");
    }
}