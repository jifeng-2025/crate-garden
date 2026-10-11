using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrateGarden
{
    public sealed class CrateGardenGame : MonoBehaviour
    {
        enum Page { Home, Levels, Play, Workshop }
        Page page;
        SaveStore store;
        Puzzle puzzle;
        Level current, draft, draftBaseline;
        int levelIndex, levelTab, customPage;
        bool testing, paused, won, help, dirty;
        string toast = "", modal = "", transfer = "", brush = "#", hint = "";
        float toastUntil, elapsed, nextMove, animStart;
        int oldPlayer, movedBox = -1, oldBox = -1;
        char[][] cells;
        readonly Stack<string[]> editUndo = new Stack<string[]>();
        Font font;
        Texture2D round, circle, crateArt, deliveredArt, wallArt, playerArt;
        AudioSource audioSource;
        AudioClip stepClip, pushClip, winClip;
        GUIStyle label, button, input;
        readonly Color bg = Hex("101F24"), panel = Hex("192D32"), panel2 = Hex("223B40"), mint = Hex("A5E1BE"), white = Hex("F1F3E7"), muted = Hex("92ABA9"), amber = Hex("F1BC70");
        public static CrateGardenGame Instance { get; private set; }
        public Puzzle ActivePuzzle { get { return puzzle; } }
        public string CurrentPage { get { return page.ToString(); } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        { if (FindObjectOfType<CrateGardenGame>() == null) new GameObject("Crate Garden").AddComponent<CrateGardenGame>(); }
        void Awake()
        {
            Instance = this; Application.targetFrameRate = 60;
            store = new SaveStore(Application.persistentDataPath);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = Shape(64, 12); circle = Shape(64, 32);
            crateArt=Resources.Load<Texture2D>("GardenArt/crate");
            deliveredArt=Resources.Load<Texture2D>("GardenArt/crate_goal");
            wallArt=Resources.Load<Texture2D>("GardenArt/wall");
            playerArt=Resources.Load<Texture2D>("GardenArt/player");
            audioSource = gameObject.AddComponent<AudioSource>();
            stepClip = Tone(330, .05f); pushClip = Tone(180, .10f); winClip = Tone(660, .36f);
            if (store.Error != null) Notify(store.Error);
            NewDraft();
        }
        void OnDestroy() { if (Instance == this) Instance = null; Destroy(round); Destroy(circle); Destroy(stepClip); Destroy(pushClip); Destroy(winClip); }
        static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#" + value, out c); return c; }
        Texture2D Shape(int size, int radius)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false); t.filterMode = FilterMode.Bilinear;
            for (int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-x,0,x-(size-1-radius)), dy=Mathf.Max(radius-y,0,y-(size-1-radius));
                t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy))));
            }
            t.Apply(); return t;
        }
        AudioClip Tone(float frequency,float length)
        {
            int n=(int)(44100*length); var data=new float[n];
            for(int i=0;i<n;i++) data[i]=Mathf.Sin(i*frequency*2*Mathf.PI/44100)*.13f*(1-i/(float)n);
            var clip=AudioClip.Create("Garden tone",n,1,44100,false); clip.SetData(data,0); return clip;
        }
        void Sound(AudioClip clip) { if (store.Progress.sound) audioSource.PlayOneShot(clip); }
        void Update()
        {
            if(page!=Page.Play || modal!="" || help) return;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(testing) ReturnToDraft(); else paused=!paused; return; }
            if(paused) return;
            if(!won) elapsed+=Time.unscaledDeltaTime;
            if(Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }
            if(Input.GetKeyDown(KeyCode.Y)) { Redo(); return; }
            if(Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if(won) return;
            int dx=0,dy=0;
            if(Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.A)) dx=-1;
            else if(Input.GetKey(KeyCode.RightArrow)||Input.GetKey(KeyCode.D)) dx=1;
            else if(Input.GetKey(KeyCode.UpArrow)||Input.GetKey(KeyCode.W)) dy=-1;
            else if(Input.GetKey(KeyCode.DownArrow)||Input.GetKey(KeyCode.S)) dy=1;
            if(dx==0 && dy==0) { nextMove=0; return; }
            bool pressed=Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.D);
            if(pressed||Time.unscaledTime>=nextMove) { Move(dx,dy); nextMove=Time.unscaledTime+(pressed?.22f:.13f); }
        }
        public void StartLevel(int index) { levelIndex=index; testing=false; StartPuzzle(Campaign.Levels[index]); }
        void StartPuzzle(Level level)
        {
            current=level.Copy(); puzzle=new Puzzle(current); page=Page.Play; paused=false; won=puzzle.Won; elapsed=0; nextMove=0; hint=""; oldPlayer=puzzle.Player; movedBox=oldBox=-1;
            if(won && !testing) { store.Complete(current.id,0,0); if(store.Error!=null) Notify(store.Error); }
        }
        public void Move(int dx,int dy)
        {
            if(paused || won) return;
            oldPlayer=puzzle.Player; int target=puzzle.Neighbour(oldPlayer,dx,dy); bool pushing=puzzle.Boxes.Contains(target);
            if(!puzzle.Move(dx,dy)) return;
            oldBox=pushing?target:-1; movedBox=pushing?puzzle.Neighbour(target,dx,dy):-1; animStart=Time.unscaledTime;
            Sound(pushing?pushClip:stepClip); hint="";
            if(puzzle.Won)
            {
                won=true; Sound(winClip);
                if(!testing) store.Complete(current.id,puzzle.Moves,puzzle.Pushes);
                if(store.Error!=null) Notify(store.Error);
            }
        }
        void Undo() { if(puzzle.Undo()) { won=false; oldPlayer=puzzle.Player; movedBox=-1; hint=""; } }
        void Redo() { if(puzzle.Redo()) { oldPlayer=puzzle.Player; movedBox=-1; won=puzzle.Won; if(won&&!testing) store.Complete(current.id,puzzle.Moves,puzzle.Pushes); } }
        void Restart() { StartPuzzle(current); }
        void Notify(string s) { toast=s; toastUntil=Time.unscaledTime+5; }
        void NewDraft()
        {
            draft=new Level("custom-"+Guid.NewGuid().ToString("N"),"My garden","A garden of your own.","##########","#        #","#        #","#  @$ .  #","#        #","#        #","#        #","##########");
            LoadDraft(draft); dirty=false;
        }
        void LoadDraft(Level level) { draftBaseline=level.Copy(); draft=level.Copy(); cells=draft.rows.Select(r=>r.ToCharArray()).ToArray(); editUndo.Clear(); dirty=false; }
        string[] Rows() { return cells.Select(r=>new string(r)).ToArray(); }
        void SyncDraft() { draft.rows=Rows(); }
        void DiscardDraft() { LoadDraft(draftBaseline); }
        void ReturnToDraft() { page=Page.Workshop; testing=paused=won=false; }
        void InitStyles()
        {
            if(label!=null) return;
            label=new GUIStyle(GUI.skin.label) { font=font, fontSize=18, wordWrap=true, richText=false };
            button=new GUIStyle(GUI.skin.button) { font=font, fontSize=18, alignment=TextAnchor.MiddleCenter, border=new RectOffset(12,12,12,12) };
            button.normal.background=round; button.hover.background=round; button.active.background=round; button.focused.background=round;
            button.normal.textColor=white; button.hover.textColor=white; button.active.textColor=white; button.focused.textColor=white;
            input=new GUIStyle(GUI.skin.textField) { font=font,fontSize=20,padding=new RectOffset(12,12,10,10) };
            input.normal.textColor=white; input.focused.textColor=white; input.normal.background=round; input.focused.background=round;
        }
        void Box(Rect r, Color color, bool disc=false) { GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,disc?Mathf.Min(r.width,r.height)/2:Mathf.Min(12,Mathf.Min(r.width,r.height)/5)); }
        void Text(Rect r,string text,int size=18,Color? color=null,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        { label.fontSize=size; label.normal.textColor=color??white; label.alignment=align; label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; GUI.Label(r,text,label); }
        bool Button(Rect r,string text,bool primary=false,bool enabled=true)
        {
            bool was=GUI.enabled; GUI.enabled=was&&enabled; GUI.backgroundColor=primary?mint:panel2; button.normal.textColor=primary?bg:white; button.hover.textColor=button.normal.textColor; button.active.textColor=button.normal.textColor;
            bool hit=GUI.Button(r,text,button); GUI.backgroundColor=Color.white; GUI.enabled=was; return hit;
        }
        void OnGUI()
        {
            InitStyles(); float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,bg,0,0);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            bool overlay=modal!=""||help; GUI.enabled=!overlay;
            Header();
            if(page==Page.Home) Home(); else if(page==Page.Levels) Levels(); else if(page==Page.Play) Play(); else Workshop();
            GUI.enabled=true;
            if(help) Help(); if(modal!="") Modal();
            if(Time.unscaledTime<toastUntil) { Box(new Rect(310,827,820,48),panel2); Text(new Rect(330,837,780,32),toast,17,mint,TextAnchor.MiddleCenter); }
            GUI.matrix=Matrix4x4.identity;
        }
        void Header()
        {
            Box(new Rect(48,32,42,42),mint); Text(new Rect(48,32,42,42),"C",26,bg,TextAnchor.MiddleCenter,true);
            Text(new Rect(104,31,350,30),"CRATE GARDEN",24,white,TextAnchor.MiddleLeft,true);
            Text(new Rect(105,62,330,24),"A little space to think.",13,muted);
            if(Button(new Rect(1120,35,125,40),store.Progress.sound?"Sound: on":"Sound: off")) { store.Progress.sound=!store.Progress.sound; if(!store.Save()) Notify(store.Error); }
            if(Button(new Rect(1260,35,130,40),"How to play")) help=true;
        }
        void Home()
        {
            Text(new Rect(75,180,640,38),"SLOW DOWN. THINK AHEAD.",16,mint);
            Text(new Rect(70,228,640,170),"Small crates.\nBeautiful puzzles.",64,white,TextAnchor.UpperLeft,true);
            Text(new Rect(75,420,500,80),"Find a way through twelve handcrafted gardens.\nPut every crate in its place, one move at a time.",22,muted);
            int next=Array.FindIndex(Campaign.Levels,l=>store.Best(l.id)==null); if(next<0) next=0;
            if(Button(new Rect(75,540,295,62),store.Progress.scores.Count==0?"Enter the garden  >":"Continue journey  >",true)) StartLevel(next);
            if(Button(new Rect(75,620,142,50),"All levels")) { page=Page.Levels; levelTab=0; }
            if(Button(new Rect(230,620,180,50),"Level workshop")) page=Page.Workshop;
            Text(new Rect(75,733,500,40),Campaign.Levels.Count(l=>store.Best(l.id)!=null)+" / 12 gardens completed     •     No timer pressure",16,muted);
            Box(new Rect(730,165,640,560),panel);
            DrawBoard(new Puzzle(Campaign.Levels[6]),new Rect(770,200,560,460),false);
            Box(new Rect(850,720,400,56),panel2); Text(new Rect(860,733,380,30),"PUSH  /  PLAN  /  FIND YOUR FLOW",15,mint,TextAnchor.MiddleCenter);
            Footer("Made for curious minds.   Built with Unity.");
        }
        void Footer(string s) { Text(new Rect(60,862,1320,26),s,13,muted,TextAnchor.MiddleCenter); }
        void Levels()
        {
            if(Button(new Rect(60,116,120,44),"< Home")) page=Page.Home;
            Text(new Rect(60,185,900,62),"Choose your next garden",40,white,TextAnchor.UpperLeft,true);
            if(Button(new Rect(990,120,185,44),"The journey",levelTab==0)) levelTab=0;
            if(Button(new Rect(1185,120,195,44),"Your creations",levelTab==1)) levelTab=1;
            var list=levelTab==0?Campaign.Levels:store.Custom.levels.ToArray(); int offset=levelTab==0?0:customPage*12;
            if(list.Length==0) Text(new Rect(60,280,1100,80),"Your collection is waiting. Create a puzzle in the Level Workshop.",24,muted);
            for(int j=0;j<12&&j+offset<list.Length;j++)
            {
                int i=j+offset; Level l=list[i]; float x=60+(j%4)*334,y=273+(j/4)*168;
                Box(new Rect(x,y,316,150),panel); Score best=store.Best(l.id);
                Text(new Rect(x+20,y+14,270,26),(levelTab==0?(i+1).ToString("00"):"CUSTOM")+(best!=null?"    /    COMPLETE":""),14,best!=null?mint:muted);
                Text(new Rect(x+20,y+46,280,35),l.title,23,white,TextAnchor.UpperLeft,true);
                Text(new Rect(x+20,y+105,170,24),best==null?"A fresh challenge":best.moves+" moves / "+best.pushes+" pushes",13,muted);
                if(Button(new Rect(x+215,y+96,82,37),"Play",true)) { if(levelTab==0) StartLevel(i); else { levelIndex=-1; testing=false; StartPuzzle(l); } }
            }
            if(Button(new Rect(60,803,235,45),"Open level workshop")) page=Page.Workshop;
            if(levelTab==1&&list.Length>12)
            { if(Button(new Rect(1080,803,130,42),"< Previous",false,customPage>0)) customPage--; if(Button(new Rect(1225,803,130,42),"Next >",false,(customPage+1)*12<list.Length)) customPage++; }
        }
        void Play()
        {
            if(Button(new Rect(60,115,150,44),testing?"< Workshop":"< Levels")) { if(testing) ReturnToDraft(); else page=Page.Levels; }
            Text(new Rect(235,115,750,45),(testing?"PLAYTEST":levelIndex>=0?"GARDEN "+(levelIndex+1).ToString("00"):"YOUR GARDEN")+"  /  "+current.title,27,white,TextAnchor.MiddleLeft,true);
            if(Button(new Rect(1215,115,165,44),paused?"Resume":"Pause")) paused=!paused;
            Box(new Rect(60,195,945,602),panel); DrawBoard(puzzle,new Rect(90,218,885,550),true);
            Box(new Rect(1030,195,350,602),panel);
            Text(new Rect(1055,218,300,30),"MAKE ROOM FOR IDEAS",14,mint);
            Text(new Rect(1055,266,285,65),puzzle.Placed+" / "+puzzle.Boxes.Count,42,white,TextAnchor.UpperLeft,true);
            Text(new Rect(1055,324,280,28),"CRATES DELIVERED",13,muted);
            Text(new Rect(1055,377,280,32),puzzle.Moves+" moves    /    "+puzzle.Pushes+" pushes",21);
            Text(new Rect(1055,428,295,98),hint!=""?hint:current.hint,18,muted);
            if(Button(new Rect(1055,544,140,46),"Undo  Z",false,puzzle.CanUndo)) Undo();
            if(Button(new Rect(1210,544,140,46),"Redo  Y",false,puzzle.CanRedo)) Redo();
            if(Button(new Rect(1055,604,295,46),"Restart garden  R")) Restart();
            if(Button(new Rect(1055,666,295,46),"A gentle hint"))
            {
                string solution=puzzle.Solve(30000);
                hint=solution==null?"Try keeping a path behind every crate.":solution.Length==0?"This position has no solution. Undo a few moves.":"Try moving "+(solution[0]=='U'?"up":solution[0]=='D'?"down":solution[0]=='L'?"left":"right")+" next.";
            }
            if(puzzle.HasCornerDeadlock()&&!won) Text(new Rect(92,804,920,42),"A crate is stuck in a corner. Undo is always available.",18,amber,TextAnchor.MiddleCenter);
            Footer("WASD / Arrow keys   Move       Z   Undo       Y   Redo       R   Restart       Esc   Pause");
            if(paused||won) PlayOverlay();
        }
        void PlayOverlay()
        {
            Box(new Rect(60,195,945,602),new Color(bg.r,bg.g,bg.b,.95f));
            Text(new Rect(155,330,750,45),won?"EVERYTHING IN ITS PLACE":"TAKE A BREATH",16,mint,TextAnchor.MiddleCenter);
            Text(new Rect(155,395,750,70),won?"Garden complete.":"Your garden can wait.",48,white,TextAnchor.MiddleCenter,true);
            Text(new Rect(155,478,750,42),won?puzzle.Moves+" moves  •  "+puzzle.Pushes+" pushes  •  "+TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss"):"Your next move will be here when you are ready.",21,muted,TextAnchor.MiddleCenter);
            if(won)
            {
                bool next=!testing&&levelIndex>=0&&levelIndex<Campaign.Levels.Length-1;
                if(Button(new Rect(355,560,355,56),testing?"Return to workshop":next?"Next garden  >":levelIndex==11?"Journey complete — all levels":"Back to collection",true))
                { if(testing) ReturnToDraft(); else if(next) StartLevel(levelIndex+1); else page=Page.Levels; }
                if(Button(new Rect(355,631,170,44),"Play again")) Restart();
                if(Button(new Rect(540,631,170,44),"Undo last move")) Undo();
            }
            else if(Button(new Rect(355,560,355,56),"Back to the garden",true)) paused=false;
        }
        void DrawBoard(Puzzle p,Rect area,bool animated)
        {
            float tile=Mathf.Min(area.width/p.Width,area.height/p.Height), ox=area.x+(area.width-tile*p.Width)/2, oy=area.y+(area.height-tile*p.Height)/2;
            for(int y=0;y<p.Height;y++) for(int x=0;x<p.Width;x++)
            { int i=y*p.Width+x; Tile(new Rect(ox+x*tile,oy+y*tile,tile,tile),p.Walls[i]?'#':p.Goals[i]?'.':' '); }
            float t=animated?Mathf.SmoothStep(0,1,(Time.unscaledTime-animStart)/.12f):1;
            foreach(int b in p.Boxes)
            {
                Vector2 pos=new Vector2(b%p.Width,b/p.Width);
                if(animated&&b==movedBox) pos=Vector2.Lerp(new Vector2(oldBox%p.Width,oldBox/p.Width),pos,t);
                Crate(new Rect(ox+pos.x*tile,oy+pos.y*tile,tile,tile),p.Goals[b]);
            }
            Vector2 player=new Vector2(p.Player%p.Width,p.Player/p.Width);
            if(animated) player=Vector2.Lerp(new Vector2(oldPlayer%p.Width,oldPlayer/p.Width),player,t);
            PlayerIcon(new Rect(ox+player.x*tile,oy+player.y*tile,tile,tile));
        }
        void Tile(Rect r,char c)
        {
            float s=r.width;
            if(c=='#' && wallArt!=null) { GUI.DrawTexture(new Rect(r.x-s*.1f,r.y-s*.1f,s*1.2f,s*1.2f),wallArt,ScaleMode.ScaleToFit,true); return; }
            if(c=='#') { Box(new Rect(r.x+3,r.y+7,s-6,s-8),Hex("102126")); Box(new Rect(r.x+3,r.y+2,s-6,s-9),Hex("466363")); Box(new Rect(r.x+8,r.y+6,s-16,4),Hex("587674")); }
            else { Box(new Rect(r.x+2,r.y+2,s-4,s-4),Hex("284348")); if(c=='.'||c=='*'||c=='+') { Box(new Rect(r.x+s*.28f,r.y+s*.28f,s*.44f,s*.44f),mint,true); Box(new Rect(r.x+s*.37f,r.y+s*.37f,s*.26f,s*.26f),Hex("284348"),true); } }
        }
        void Crate(Rect r,bool goal)
        {
            float s=r.width; Color c=goal?mint:amber;
            Texture2D art=goal?deliveredArt:crateArt;
            if(art!=null) { Box(new Rect(r.x+s*.19f,r.y+s*.65f,s*.62f,s*.23f),Hex("152B2D"),true); GUI.DrawTexture(r,art,ScaleMode.ScaleToFit,true); return; }
            Box(new Rect(r.x+s*.13f,r.y+s*.19f,s*.74f,s*.72f),Hex("172D30"));
            Box(new Rect(r.x+s*.13f,r.y+s*.1f,s*.74f,s*.73f),c);
            Box(new Rect(r.x+s*.23f,r.y+s*.2f,s*.54f,s*.5f),goal?Hex("72B899"):Hex("C88E4C"));
            Box(new Rect(r.x+s*.29f,r.y+s*.25f,s*.42f,s*.4f),c);
            Text(new Rect(r.x,r.y+s*.08f,s,s*.68f),goal?"+":"/",Mathf.RoundToInt(s*.42f),goal?Hex("477F68"):Hex("A5703E"),TextAnchor.MiddleCenter,true);
        }
        void PlayerIcon(Rect r)
        {
            float s=r.width;
            if(playerArt!=null) { Box(new Rect(r.x+s*.23f,r.y+s*.68f,s*.54f,s*.18f),Hex("12272B"),true); GUI.DrawTexture(r,playerArt,ScaleMode.ScaleToFit,true); return; }
            Box(new Rect(r.x+s*.22f,r.y+s*.26f,s*.56f,s*.58f),Hex("12272B"),true);
            Box(new Rect(r.x+s*.19f,r.y+s*.12f,s*.62f,s*.62f),white,true);
            Box(new Rect(r.x+s*.26f,r.y+s*.39f,s*.48f,s*.3f),Hex("6295A0"),true);
            Box(new Rect(r.x+s*.33f,r.y+s*.47f,s*.09f,s*.09f),bg,true); Box(new Rect(r.x+s*.57f,r.y+s*.47f,s*.09f,s*.09f),bg,true);
        }
        void Workshop()
        {
            if(Button(new Rect(60,115,135,44),"< Home")) { if(dirty) modal="leave"; else page=Page.Home; }
            Text(new Rect(220,115,750,45),"Level workshop",32,white,TextAnchor.MiddleLeft,true);
            if(Button(new Rect(1215,115,165,44),"Playtest  >",true))
            {
                SyncDraft(); string error=Puzzle.Validate(draft);
                if(error!=null) Notify(error); else { testing=true; levelIndex=-1; StartPuzzle(draft); }
            }
            Box(new Rect(60,195,945,602),panel); Box(new Rect(1030,195,350,602),panel);
            Text(new Rect(1055,212,295,30),"GARDEN NAME",13,mint);
            GUI.backgroundColor=panel2;
            string title=GUI.TextField(new Rect(1055,247,295,45),draft.title,32,input); if(title!=draft.title) { draft.title=title; dirty=true; }
            GUI.backgroundColor=Color.white;
            string[] chars={"#"," ",".","$","@"}, names={"Wall","Floor","Goal","Crate","Player"};
            for(int i=0;i<5;i++) if(Button(new Rect(1055+(i%2)*153,314+(i/2)*52,i==4?295:142,43),names[i],brush==chars[i])) brush=chars[i];
            if(Button(new Rect(1055,487,142,43),"Undo paint",false,editUndo.Count>0)) { cells=editUndo.Pop().Select(r=>r.ToCharArray()).ToArray(); dirty=true; }
            if(Button(new Rect(1208,487,142,43),"Validate"))
            {
                SyncDraft(); string error=Puzzle.Validate(draft);
                if(error!=null) Notify(error); else { string s=new Puzzle(draft).Solve(30000); Notify(s==null?"Structure valid. Solver budget reached; playtest to confirm.":s.Length==0&&!new Puzzle(draft).Won?"This garden cannot be solved. Keep editing.":"Valid and solvable! "+s.Length+" moves in one solution."); }
            }
            if(Button(new Rect(1055,549,295,46),"Save to collection",true)) SaveDraft();
            if(Button(new Rect(1055,610,142,43),"Load saved")) modal="load";
            if(Button(new Rect(1208,610,142,43),"New garden")) modal="new";
            if(Button(new Rect(1055,665,142,43),"Import")) { transfer=""; modal="import"; }
            if(Button(new Rect(1208,665,142,43),"Export")) { SyncDraft(); transfer=JsonUtility.ToJson(draft,true); modal="export"; }
            Text(new Rect(1055,734,295,35),dirty?"Unsaved changes":"Draft ready",15,dirty?amber:muted);
            float tile=Mathf.Min(860f/cells[0].Length,530f/cells.Length),ox=100+(860-tile*cells[0].Length)/2,oy=226+(530-tile*cells.Length)/2;
            Event e=Event.current;
            for(int y=0;y<cells.Length;y++) for(int x=0;x<cells[y].Length;x++)
            {
                Rect r=new Rect(ox+x*tile,oy+y*tile,tile,tile); char c=cells[y][x]; Tile(r,c);
                if(c=='$'||c=='*') Crate(r,c=='*'); if(c=='@'||c=='+') PlayerIcon(r);
                if(GUI.enabled&&r.Contains(e.mousePosition)&&e.type==EventType.MouseDown&&(e.button==0||e.button==1))
                {
                    if(x==0||y==0||x==cells[y].Length-1||y==cells.Length-1) Notify("The border stays closed to keep your garden safe.");
                    else { editUndo.Push(Rows()); Paint(x,y,e.button==1?' ':brush[0]); dirty=true; }
                    e.Use();
                }
            }
            Footer("Click to paint   /   Right-click to erase   /   Place goals under crates by painting Goal, then Crate");
        }
        void Paint(int x,int y,char tool)
        {
            char c=cells[y][x]; bool goal=c=='.'||c=='*'||c=='+';
            if(tool=='@')
            {
                for(int j=0;j<cells.Length;j++) for(int i=0;i<cells[j].Length;i++) if(cells[j][i]=='@'||cells[j][i]=='+') cells[j][i]=cells[j][i]=='+'?'.':' ';
                cells[y][x]=goal?'+':'@';
            }
            else if(tool=='$') cells[y][x]=goal?'*':'$';
            else if(tool=='.') cells[y][x]=c=='$'||c=='*'?'*':c=='@'||c=='+'?'+':'.';
            else cells[y][x]=tool;
        }
        void SaveDraft()
        {
            SyncDraft(); string error=Puzzle.Validate(draft);
            if(error!=null) { Notify(error); return; }
            if(string.IsNullOrWhiteSpace(draft.title)) { Notify("Give your garden a name first."); return; }
            store.SaveLevel(draft); if(store.Error!=null) Notify(store.Error); else { draftBaseline=draft.Copy(); dirty=false; Notify("Saved to Your creations."); }
        }
        void Scrim() { Box(new Rect(0,0,1440,900),new Color(.025f,.07f,.08f,.96f)); }
        void Help()
        {
            Scrim(); Box(new Rect(290,170,860,580),panel);
            Text(new Rect(335,210,750,60),"A little guide to the garden",36,white,TextAnchor.UpperLeft,true);
            Text(new Rect(335,305,750,285),"01   Put every wooden crate on a mint ring.\n\n02   Move with arrow keys or WASD. Push one crate at a time.\n        You cannot pull crates or push two together.\n\n03   Avoid corners without goals. Undo with Z, redo with Y.\n\n04   Build your own puzzles in the workshop, save and playtest.\n        Import / export JSON to share your creations.",22,muted);
            if(Button(new Rect(335,650,750,55),"Let's grow something",true)) help=false;
        }
        Vector2 loadScroll;
        void Modal()
        {
            Scrim(); Box(new Rect(260,130,920,655),panel);
            Text(new Rect(300,160,800,50),modal=="import"?"Import a garden":modal=="export"?"Share your garden":modal=="load"?"Your saved gardens":modal=="new"?"Start a fresh garden?":"Leave the workshop?",34,white,TextAnchor.UpperLeft,true);
            if(modal=="import"||modal=="export")
            {
                Text(new Rect(300,225,820,36),modal=="import"?"Paste a level JSON below. Your current draft will be replaced.":"Copy this JSON to share. Import it on another device to play.",18,muted);
                GUI.backgroundColor=panel2; transfer=GUI.TextArea(new Rect(300,277,840,355),transfer,24000,input); GUI.backgroundColor=Color.white;
                if(Button(new Rect(300,660,405,52),modal=="import"?"Import level":"Copy to clipboard",true))
                {
                    if(modal=="export") { GUIUtility.systemCopyBuffer=transfer; Notify("Level JSON copied."); }
                    else try
                    {
                        Level imported=JsonUtility.FromJson<Level>(transfer); string error=Puzzle.Validate(imported);
                        if(error!=null) Notify(error); else
                        { imported.id="custom-"+Guid.NewGuid().ToString("N"); if(string.IsNullOrWhiteSpace(imported.title)) imported.title="Imported garden"; if(imported.title.Length>32) imported.title=imported.title.Substring(0,32); LoadDraft(imported); dirty=true; modal=""; Notify("Imported. Playtest and save your garden."); }
                    } catch(Exception) { Notify("Invalid JSON. Please paste a complete exported level."); }
                }
                if(Button(new Rect(725,660,415,52),"Close")) modal="";
            }
            else if(modal=="load")
            {
                Text(new Rect(300,220,800,35),"Loading replaces the current draft. Save your changes before loading.",17,muted);
                loadScroll=GUI.BeginScrollView(new Rect(300,280,840,350),loadScroll,new Rect(0,0,800,Mathf.Max(340,store.Custom.levels.Count*66)));
                for(int i=0;i<store.Custom.levels.Count;i++)
                { Level l=store.Custom.levels[i]; if(Button(new Rect(0,i*66,790,54),l.title)) { LoadDraft(l); modal=""; } }
                GUI.EndScrollView();
                if(store.Custom.levels.Count==0) Text(new Rect(325,330,700,70),"No saved gardens yet. Save your first creation to see it here.",23,muted);
                if(Button(new Rect(300,665,840,52),"Cancel")) modal="";
            }
            else
            {
                Text(new Rect(300,270,790,145),dirty?"You have unsaved changes. Continue editing to save them, or discard this draft.":"Your saved gardens will remain in your collection.",25,muted);
                if(Button(new Rect(300,515,405,60),modal=="new"?"Create new garden":"Leave workshop",true)) { if(modal=="new") NewDraft(); else { DiscardDraft(); page=Page.Home; } modal=""; }
                if(Button(new Rect(725,515,415,60),"Keep editing")) modal="";
            }
        }
    }
}
