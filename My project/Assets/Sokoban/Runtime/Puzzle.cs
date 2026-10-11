using System;
using System.Collections.Generic;
using System.Linq;

namespace CrateGarden
{
    [Serializable] public class Level
    {
        public string id, title, hint;
        public string[] rows;
        public Level(string id, string title, string hint, params string[] rows)
        { this.id = id; this.title = title; this.hint = hint; this.rows = rows; }
        public Level Copy() { return new Level(id, title, hint, (string[])rows.Clone()); }
    }

    // Pure deterministic rules: no rendering, input, persistence or Unity dependencies.
    public sealed class Puzzle
    {
        public readonly int Width, Height;
        public readonly bool[] Walls, Goals;
        public HashSet<int> Boxes { get; private set; }
        public int Player { get; private set; }
        public int Moves { get; private set; }
        public int Pushes { get; private set; }
        readonly Stack<Snapshot> undo = new Stack<Snapshot>(), redo = new Stack<Snapshot>();
        struct Snapshot { public int player, moves, pushes; public HashSet<int> boxes; }
        public bool Won { get { return Boxes.Count > 0 && Boxes.All(b => Goals[b]); } }
        public int Placed { get { return Boxes.Count(b => Goals[b]); } }
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public Puzzle(Level level)
        {
            string error = Validate(level);
            if (error != null) throw new ArgumentException(error);
            Height = level.rows.Length; Width = level.rows[0].Length;
            Walls = new bool[Width * Height]; Goals = new bool[Width * Height]; Boxes = new HashSet<int>();
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int p = y * Width + x; char c = level.rows[y][x];
                Walls[p] = c == '#'; Goals[p] = c == '.' || c == '*' || c == '+';
                if (c == '$' || c == '*') Boxes.Add(p);
                if (c == '@' || c == '+') Player = p;
            }
        }
        public static string Validate(Level level)
        {
            if (level == null || level.rows == null || level.rows.Length < 3 || level.rows.Length > 16) return "Height must be 3-16 tiles.";
            int w = level.rows[0] == null ? 0 : level.rows[0].Length, players = 0, boxes = 0, goals = 0;
            if (w < 3 || w > 20) return "Width must be 3-20 tiles.";
            for (int y = 0; y < level.rows.Length; y++)
            {
                if (level.rows[y] == null || level.rows[y].Length != w) return "Every row must have the same width.";
                for (int x = 0; x < w; x++)
                {
                    char c = level.rows[y][x];
                    if ("# .$@*+".IndexOf(c) < 0) return "Unknown tile: " + c;
                    if ((x == 0 || y == 0 || x == w - 1 || y == level.rows.Length - 1) && c != '#') return "Close the outer border with walls.";
                    if (c == '@' || c == '+') players++;
                    if (c == '$' || c == '*') boxes++;
                    if (c == '.' || c == '*' || c == '+') goals++;
                }
            }
            if (players != 1) return "Place exactly one player.";
            if (boxes == 0 || boxes != goals) return "Use the same number of crates and goals (at least one).";
            return null;
        }
        Snapshot Capture() { return new Snapshot { player = Player, moves = Moves, pushes = Pushes, boxes = new HashSet<int>(Boxes) }; }
        void Restore(Snapshot s) { Player = s.player; Moves = s.moves; Pushes = s.pushes; Boxes = new HashSet<int>(s.boxes); }
        public int Neighbour(int p, int dx, int dy)
        {
            int x = p % Width + dx, y = p / Width + dy;
            return x < 0 || y < 0 || x >= Width || y >= Height ? -1 : y * Width + x;
        }
        public bool Move(int dx, int dy)
        {
            if (Math.Abs(dx) + Math.Abs(dy) != 1 || Won) return false;
            int to = Neighbour(Player, dx, dy);
            if (to < 0 || Walls[to]) return false;
            bool push = Boxes.Contains(to); int next = Neighbour(to, dx, dy);
            if (push && (next < 0 || Walls[next] || Boxes.Contains(next))) return false;
            undo.Push(Capture()); redo.Clear();
            if (push) { Boxes.Remove(to); Boxes.Add(next); Pushes++; }
            Player = to; Moves++; return true;
        }
        public bool Undo() { if (!CanUndo) return false; redo.Push(Capture()); Restore(undo.Pop()); return true; }
        public bool Redo() { if (!CanRedo) return false; undo.Push(Capture()); Restore(redo.Pop()); return true; }
        public bool HasCornerDeadlock()
        {
            foreach (int b in Boxes)
            {
                if (Goals[b]) continue;
                if ((Blocked(b, -1, 0) || Blocked(b, 1, 0)) && (Blocked(b, 0, -1) || Blocked(b, 0, 1))) return true;
            }
            return false;
        }
        bool Blocked(int p, int dx, int dy) { int n = Neighbour(p, dx, dy); return n < 0 || Walls[n]; }
        // Bounded breadth-first search. Null means budget exhausted; empty means proven unsolvable.
        public string Solve(int budget = 120000)
        {
            var queue = new Queue<Tuple<int, HashSet<int>, string>>();
            var seen = new HashSet<string>();
            queue.Enqueue(Tuple.Create(Player, new HashSet<int>(Boxes), ""));
            int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 }; string directions = "URDL";
            while (queue.Count > 0)
            {
                if (seen.Count >= budget) return null;
                var s = queue.Dequeue();
                if (s.Item2.All(b => Goals[b])) return s.Item3;
                for (int d = 0; d < 4; d++)
                {
                    int n = Neighbour(s.Item1, dx[d], dy[d]); if (n < 0 || Walls[n]) continue;
                    var b = new HashSet<int>(s.Item2);
                    if (b.Contains(n))
                    {
                        int t = Neighbour(n, dx[d], dy[d]); if (t < 0 || Walls[t] || b.Contains(t)) continue;
                        if (!Goals[t] && (Blocked(t,-1,0)||Blocked(t,1,0)) && (Blocked(t,0,-1)||Blocked(t,0,1))) continue;
                        b.Remove(n); b.Add(t);
                    }
                    string key = n + ":" + string.Join(",", b.OrderBy(v => v));
                    if (seen.Add(key)) queue.Enqueue(Tuple.Create(n, b, s.Item3 + directions[d]));
                }
            }
            return "";
        }
    }
    public static class Campaign
    {
        public static readonly Level[] Levels = {
            new Level("01","First delivery","Push the crate onto the mint goal. Arrows or WASD to move.","#######","#     #","# @$ .#","#     #","#######"),
            new Level("02","Around the corner","You can push, but never pull. Walk around to change direction.","#######","#    .#","#  $  #","#  @  #","#     #","#######"),
            new Level("03","Two deliveries","Every crate needs a goal. Plan where you will stand next.","########","# .  . #","# $  $ #","#   @  #","#      #","########"),
            new Level("04","The alcove","A crate in a bare corner cannot move. Use Z to undo.","########","#   # .#","#   $  #","# @ #  #","#      #","########"),
            new Level("05","Crossing paths","Deliver both crates without blocking the way around.","########","#  .   #","#  $ . #","#  $   #","# @    #","#      #","########"),
            new Level("06","Narrow passage","The center column changes how you approach each crate.","#########","# . # . #","#   #   #","# $   $ #","#   @   #","#       #","#########"),
            new Level("07","Three wishes","Three crates, three goals. Keep a walking route open.","#########","# . . . #","#       #","# $ $ $ #","#   @   #","#       #","#########"),
            new Level("08","Side entrance","Approach the lower crate from the correct side.","########","# .    #","# # $  #","#   #. #","# $    #","#  @   #","########"),
            new Level("09","The courtyard","Use the open courtyard to reposition before pushing.","#########","#  . .  #","# #   # #","#  $ $  #","#   $   #","#   @ . #","#       #","#########"),
            new Level("10","Split garden","Two wings share one central route.","##########","# .    . #","#   ##   #","# $    $ #","#   ##   #","#    @   #","##########"),
            new Level("11","Delivery order","The wall makes the order of your pushes matter.","#########","# . .   #","#   # . #","# $ $ $ #","#   #   #","#   @   #","#       #","#########"),
            new Level("12","Garden keeper","Your final delivery. Take your time; every move is reversible.","##########","# . . .  #","#        #","# $ # $  #","#   $    #","#  #  #  #","#    @   #","##########")
        };
    }
}
