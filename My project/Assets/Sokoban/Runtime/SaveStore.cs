using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CrateGarden
{
    [Serializable] public class Score { public string id; public int moves, pushes; }
    [Serializable] public class Progress { public int version = 1; public bool sound = true; public List<Score> scores = new List<Score>(); }
    [Serializable] public class LevelCollection { public int version = 1; public List<Level> levels = new List<Level>(); }
    public sealed class SaveStore
    {
        readonly string root;
        public Progress Progress = new Progress();
        public LevelCollection Custom = new LevelCollection();
        public string Error { get; private set; }
        public SaveStore(string directory) { root = directory; Load(); }
        T Read<T>(string name) where T : new()
        {
            string path = Path.Combine(root, name);
            if (!File.Exists(path)) return new T();
            try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
            catch (Exception)
            {
                if (File.Exists(path + ".bak"))
                {
                    try { var recovered = JsonUtility.FromJson<T>(File.ReadAllText(path + ".bak")); Error = "Recovered a previous save from backup."; return recovered; }
                    catch (Exception) { }
                }
                Error = "A damaged save could not be loaded. The original file was kept.";
                return new T();
            }
        }
        void Load()
        {
            try
            {
                Directory.CreateDirectory(root);
                Progress = Read<Progress>("progress.json") ?? new Progress();
                Custom = Read<LevelCollection>("levels.json") ?? new LevelCollection();
                if (Progress.scores == null) Progress.scores = new List<Score>();
                Progress.scores.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
                if (Custom.levels == null) Custom.levels = new List<Level>();
                Custom.levels.RemoveAll(v => Puzzle.Validate(v) != null);
            }
            catch (Exception e) { Error = "Save could not be loaded: " + e.Message; }
        }
        void Write(string name, object data)
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            // Atomic replacement retains the previous complete file as a backup.
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public bool Save()
        {
            try { Write("progress.json", Progress); Write("levels.json", Custom); Error = null; return true; }
            catch (Exception e) { Error = "Unable to save: " + e.Message; return false; }
        }
        public Score Best(string id) { return Progress.scores.Find(s => s.id == id); }
        public void Complete(string id, int moves, int pushes)
        {
            Score best = Best(id);
            if (best == null) Progress.scores.Add(new Score { id = id, moves = moves, pushes = pushes });
            else if (moves < best.moves || (moves == best.moves && pushes < best.pushes)) { best.moves = moves; best.pushes = pushes; }
            Save();
        }
        public void SaveLevel(Level level)
        {
            int i = Custom.levels.FindIndex(v => v.id == level.id);
            if (i >= 0)
            {
                if (!Custom.levels[i].rows.SequenceEqual(level.rows)) Progress.scores.RemoveAll(s => s.id == level.id);
                Custom.levels[i] = level.Copy();
            }
            else Custom.levels.Add(level.Copy());
            Save();
        }
    }
}
