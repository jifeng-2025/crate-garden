using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using CrateGarden;

public static class ProjectTools
{
    static string Root { get { return Directory.GetParent(Application.dataPath).Parent.FullName; } }
    [MenuItem("Crate Garden/Prepare Main Scene")]
    public static void Prepare()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        var camera=Camera.main; camera.backgroundColor=new Color(.06f,.12f,.14f); camera.clearFlags=CameraClearFlags.SolidColor;
        new GameObject("Crate Garden").AddComponent<CrateGardenGame>();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/CrateGarden.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CrateGarden.unity",true)};
        PlayerSettings.companyName="Garden Studio"; PlayerSettings.productName="Crate Garden";
        PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=true;
        PlayerSettings.resizableWindow=true; PlayerSettings.colorSpace=ColorSpace.Gamma;
        AssetDatabase.SaveAssets(); Debug.Log("CRATE_GARDEN_SCENE_READY");
    }
    static int assertions;
    static void Check(bool ok,string description) { assertions++; if(!ok) throw new Exception("FAILED: "+description); }
    [MenuItem("Crate Garden/Run Acceptance Tests")]
    public static void Validate()
    {
        assertions=0; var report=new StringBuilder("# Automated acceptance results\n\n");
        try
        {
            var l=new Level("test","Test","", "#######","#     #","# @$ .#","#     #","#######");
            var p=new Puzzle(l);
            Check(!p.Move(1,1),"diagonal movement rejected"); Check(p.Moves==0,"invalid move does not count");
            Check(p.Move(1,0)&&p.Pushes==1,"crate pushes"); Check(p.Move(1,0)&&p.Won,"goal victory");
            Check(!p.Move(0,1),"victory freezes board"); Check(p.Undo()&&!p.Won&&p.Pushes==1,"undo victory");
            Check(p.Redo()&&p.Won,"redo victory"); p.Undo(); p.Move(0,1); Check(!p.CanRedo,"branch clears redo");
            p=new Puzzle(new Level("t","t","","#######","#@$$..#","#######"));
            Check(!p.Move(1,0)&&p.Moves==0,"cannot push two crates"); Check(!p.Move(0,-1),"walls block");
            p=new Puzzle(new Level("t","t","","######","#@* .#","# $  #","#    #","######"));
            Check(p.Placed==1&&!p.Won,"crate on goal parser"); Check(p.Move(1,0)&&p.Placed==0,"goal stays when crate leaves"); p.Undo(); Check(p.Placed==1,"undo restores goal occupancy");
            Check(Puzzle.Validate(new Level("t","t","","#####","#@@$#","# . #","#####"))!=null,"multiple players invalid");
            Check(Puzzle.Validate(new Level("t","t","","#####","#@$. ","#####"))!=null,"open border invalid");
            Check(Puzzle.Validate(new Level("t","t","","#####","#@  #","#####"))!=null,"empty boxes invalid");
            p=new Puzzle(new Level("t","t","","######","#@$ .#","#    #","######")); p.Move(0,1); p.Move(1,0); p.Move(1,0); p.Move(0,-1); p.Move(-1,0);
            Check(p.HasCornerDeadlock(),"corner deadlock detection");
            report.AppendLine("Core rules: PASS\n");
            foreach(var level in Campaign.Levels)
            {
                p=new Puzzle(level); string solution=p.Solve(350000);
                Check(solution!=null&&solution.Length>0,"solvable level "+level.id);
                foreach(char d in solution) Check(p.Move(d=='L'?-1:d=='R'?1:0,d=='U'?-1:d=='D'?1:0),"solution replay "+level.id);
                Check(p.Won,"solution reaches victory "+level.id);
                report.AppendLine("- "+level.id+" "+level.title+": **PASS**, "+solution.Length+" moves. Solution: `"+solution+"`");
            }
            string path=Path.Combine(Path.GetTempPath(),"crate-garden-tests-"+Guid.NewGuid().ToString("N"));
            var saves=new SaveStore(path); saves.Complete("01",10,4); saves.Complete("01",12,2); saves.SaveLevel(l);
            var loaded=new SaveStore(path); Check(loaded.Best("01").moves==10,"best score is preserved");
            Check(loaded.Custom.levels.Count==1,"custom level round trip");
            loaded.Complete("01",8,3); Check(new SaveStore(path).Best("01").moves==8,"improved score persists");
            loaded.SaveLevel(l); Check(loaded.Custom.levels.Count==1,"saving edits replaces rather than duplicates");
            loaded.Complete(l.id,2,2);
            var renamed=l.Copy(); renamed.title="Renamed"; loaded.SaveLevel(renamed);
            Check(loaded.Best(l.id)!=null,"renaming custom level preserves score");
            var changed=l.Copy(); changed.rows[1]="# #   #"; loaded.SaveLevel(changed);
            Check(loaded.Best(l.id)==null,"changed layout resets obsolete score");
            Check(new SaveStore(path).Best(l.id)==null,"score reset persists");
            string json=JsonUtility.ToJson(l); Check(Puzzle.Validate(JsonUtility.FromJson<Level>(json))==null,"export import round trip");
            File.WriteAllText(Path.Combine(path,"progress.json"),"{broken");
            var recovered=new SaveStore(path); Check(recovered.Best("01")!=null,"corrupt progress recovers backup"); Check(recovered.Custom.levels.Count==1,"corrupt progress does not lose custom levels");
            report.AppendLine("\nPersistence, backup recovery and JSON round-trip: PASS");
            foreach(string asset in new[]{"crate","crate_goal","wall","player"})
            {
                var texture=Resources.Load<Texture2D>("GardenArt/"+asset);
                Check(texture!=null && texture.width==256 && texture.height==256,"Blender texture ships: "+asset);
            }
            report.AppendLine("\nBlender art resources: all four 256x256 textures load successfully.");
            report.AppendLine("\nAssertions: "+assertions+". All passed.");
            Directory.CreateDirectory(Path.Combine(Root,"Docs")); File.WriteAllText(Path.Combine(Root,"Docs/TEST_RESULTS.md"),report.ToString());
            Debug.Log("CRATE_GARDEN_TESTS_PASS assertions="+assertions);
        }
        catch(Exception e)
        {
            report.AppendLine("\nFAIL: "+e); File.WriteAllText(Path.Combine(Root,"Docs/TEST_RESULTS.md"),report.ToString()); Debug.LogException(e); throw;
        }
    }
    [MenuItem("Crate Garden/Build macOS")]
    public static void BuildMac()
    {
        var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/CrateGarden.unity"},locationPathName=Path.Combine(Root,"Builds/Crate Garden.app"),target=BuildTarget.StandaloneOSX,options=BuildOptions.None });
        File.WriteAllText(Path.Combine(Root,"Docs/BUILD_RESULT.md"),"# macOS build\n\nResult: "+r.summary.result+"\nErrors: "+r.summary.totalErrors+"\nWarnings: "+r.summary.totalWarnings+"\nSize: "+r.summary.totalSize+" bytes\n");
        if(r.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed");
        Debug.Log("CRATE_GARDEN_BUILD_PASS");
    }
    [MenuItem("Crate Garden/Smoke Test Play Mode")]
    public static void SmokePlay()
    {
        if(!EditorApplication.isPlaying) throw new Exception("Enter Play mode first");
        var game=CrateGardenGame.Instance; if(game==null) throw new Exception("No game instance");
        game.StartLevel(0); game.Move(1,0); game.Move(1,0);
        if(!game.ActivePuzzle.Won) throw new Exception("Play-mode delivery failed");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var type=typeof(CrateGardenGame);
        Action<string,object[]> call=(name,args)=>type.GetMethod(name,flags).Invoke(game,args);
        Func<string,object> field=name=>type.GetField(name,flags).GetValue(game);
        // Exercise the actual editor draft lifecycle, isolated from player saves.
        call("NewDraft",new object[0]);
        var baseline=((Level)field("draft")).Copy();
        call("Paint",new object[]{2,1,'#'}); call("SyncDraft",new object[0]);
        ((Level)field("draft")).title="Unsaved title";
        call("DiscardDraft",new object[0]);
        var restored=(Level)field("draft");
        if(restored.title!=baseline.title || !restored.rows.SequenceEqual(baseline.rows)) throw new Exception("Discard did not restore baseline");
        call("Paint",new object[]{2,1,'#'}); call("SyncDraft",new object[0]);
        type.GetField("testing",flags).SetValue(game,true);
        call("StartPuzzle",new object[]{field("draft")}); call("ReturnToDraft",new object[0]);
        if(((char[][])field("cells"))[1][2]!='#') throw new Exception("Playtest lost draft edits");
        type.GetField("testing",flags).SetValue(game,true);
        call("StartPuzzle",new object[]{new Level("solved","Solved","","#####","#@* #","#####")});
        if(!(bool)field("won")) throw new Exception("Initially solved level did not show victory");
        call("NewDraft",new object[0]); game.StartLevel(0);
        File.WriteAllText(Path.Combine(Root,"Docs/PLAYMODE_RESULTS.md"),"# Play mode regression\n\nPASS: normal victory, discard restores title and grid after synchronization, playtest retains draft, initially solved level shows victory.\n");
        Debug.Log("CRATE_GARDEN_PLAYMODE_PASS including editor regressions");
    }
}
