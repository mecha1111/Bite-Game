using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
/// <summary>Rendered world/HUD separation, bounded effects, rank persistence and real-clock heavy workload.</summary>
public partial class StagePresentationChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);_checks++;}
    private async Task Frame(){GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Frames(int count){for(int i=0;i<count;i++)await Frame();}
    private async Task<Image> Capture(string name)
    {await Frames(2);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);var image=GetViewport().GetTexture().GetImage();image.SavePng("res://artifacts/stage-presentation/"+name+".png");return image;}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private GameplayScreen Spawn(string id)
    {var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId=id;AddChild(game);game.SetProcessInput(false);return game;}
    private async Task Run()
    {
        try
        {
            using(var material=new ShaderMaterial())
            using(var shader=new Shader())
            using(var name=new StringName("probe"))
            {
                shader.Code="shader_type canvas_item; uniform float probe = 0.0;";material.Shader=shader;
                for(int i=0;i<20;i++){material.SetShaderParameter("probe",1f);material.SetShaderParameter(name,1f);}
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<1000;i++)material.SetShaderParameter("probe",1f);
                long converted=GC.GetAllocatedBytesForCurrentThread()-before;before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<1000;i++)material.SetShaderParameter(name,1f);
                long cached=GC.GetAllocatedBytesForCurrentThread()-before;
                GD.Print($"UNIFORM ALLOCATIONS 1000 calls: string={converted} cached={cached} bytes");
                Check(cached<converted,"cached shader names reduce measured managed allocations");
            }
            string save="/private/tmp/bite-stage-fx-"+Time.GetTicksUsec()+".cfg";
            SaveStore.Initialize(save);ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);
            double baseline=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),orphans=Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            foreach(string id in new[]{"song_1","song_2","song_3","song_4"})
            {
                var game=Spawn(id);await Frames(5);game.SuspendForSettings();game.SetProcess(false);game.Rhythm.SetProcess(false);
                var hud=new Control[]{game.Gauge,game.GetNode<Control>("Composition/JudgmentAnchor"),game.Combo,game.GetNode<Control>("SettingsButton")};
                var rectangles=hud.Select(n=>n.GetGlobalRect()).ToArray();var mouth=game.Shark.MouthImpact.GlobalPosition;
                var targets=game.Data.Phrases.Select(p=>p.TargetSeconds).ToArray();double clock=game.Rhythm.SongTimeSeconds,value=game.Satiety.Value;
                Check(game.Presentation.Events.Length>15,id+" cached authored FX loaded before playback");
                foreach(var e in game.Presentation.Events)
                {
                    double time=e.Time+Math.Min(.04,e.Duration*.5);game.Environment.Present(time,false);game.Presentation.Present(time);
                    Check(game.Presentation.CameraZoom>=.988f-1e-6&&game.Presentation.CameraZoom<=1.040001f&&Math.Abs(game.Presentation.CameraOffset.X)<=12&&Math.Abs(game.Presentation.CameraOffset.Y)<=12,id+" bounded "+e.Type+" "+e.Time);
                }
                Check(hud.Select(n=>n.GetGlobalRect()).SequenceEqual(rectangles)&&game.Shark.MouthImpact.GlobalPosition==mouth,id+" world camera never changes HUD or mouth coordinates");
                Check(game.Rhythm.SongTimeSeconds==clock&&game.Satiety.Value==value&&game.Data.Phrases.Select(p=>p.TargetSeconds).SequenceEqual(targets),id+" FX observer never advances clock, balance or targets");
                game.Presentation.Present(game.Data.DurationSeconds+9);Check(game.Presentation.CameraZoom==1&&game.Presentation.CameraOffset==Vector2.Zero,id+" finished choreography returns to neutral");
                game.Presentation.Intensity=0;game.Presentation.Present(game.Presentation.Events[0].Time+.04);Check(game.Presentation.CameraZoom==1&&game.Presentation.CameraOffset==Vector2.Zero,id+" zero intensity disables presentation motion");
                if(id=="song_4")
                {
                    var environments=LocalCsv.Read("res://data/balance/environment_sequence.csv").Where(r=>r["song_id"]==id).ToArray();
                    Check(environments.Select(r=>r["environment_song_id"]).SequenceEqual(new[]{"song_1","song_3","song_1","song_2","song_3","song_2","song_1"}),"EX non-sequential musical remix order");
                    Check(game.Environment.ActiveEnvironmentSongId=="song_1"&&!game.Environment.PreviousBackground.Visible,"EX final transition finishes and frees previous draw");
                    game.Presentation.Intensity=1;game.Interference.Trigger("sardine",clock);game.Presentation.Present(clock);Check(game.Presentation.ReadabilityMultiplier==.35f,"active sensing-denial reduces visual intensity");
                }
                if(id=="song_1")await BubbleAndHud(game);
                game.StopGameplay();game.QueueFree();await Frames(6);
                Check(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)==baseline&&Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)==orphans,id+" transition/effect nodes return to baseline");
            }
            var tutorial=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();AddChild(tutorial);await Frames(5);
            Check(tutorial.Presentation.Events.Length==0&&!tutorial.Juice.JumpBubbles.Enabled,"tutorial keeps its existing presentation and logic");tutorial.StopGameplay();tutorial.QueueFree();await Frames(6);
            await MedalChecks(save);
            await HeavyChecks();
            GD.Print("STAGE PRESENTATION VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task BubbleAndHud(GameplayScreen game)
    {
        var bubbles=game.Juice.JumpBubbles;var atlas=(AtlasTexture)bubbles.BubbleTexture;
        Check(atlas.Atlas.ResourcePath=="res://assets/물방울.png","bubble batch uses supplied texture, not generated circles");
        double nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);game.Juice.Bite(0);
        Check(bubbles.LastEmittedCount is >=24 and <=36&&bubbles.LastOrigin==game.Shark.GlobalPosition+new Vector2(0,-28),"24–36 bubbles originate under shark");
        var array=(Array)typeof(SharkJumpBubbleEffect).GetField("_bubbles",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(bubbles)!;
        var live=array.Cast<object>().Where(b=>(bool)b.GetType().GetField("Alive")!.GetValue(b)!).ToArray();
        double Field(object b,string name)=>Convert.ToDouble(b.GetType().GetField(name)!.GetValue(b));
        Check(live.All(b=>Field(b,"Life")>=.35&&Field(b,"Life")<=.8)&&live.Select(b=>Field(b,"Size")).Distinct().Count()>10,"bubble lifetimes and mixed sizes vary");
        Check(live.All(b=>((Vector2)b.GetType().GetField("Velocity")!.GetValue(b)!).Y<0),"bubble impulses rise upward with lateral spread");
        nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);for(int i=0;i<20;i++)bubbles.Emit(.01*i,bubbles.LastOrigin);Check(bubbles.LiveCount<=256&&Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)==nodes,"dense Bite bubble batch bounded without node churn");
        bubbles.Reset();game.Juice.Bite(0);bubbles.Present(.15);(await Capture("bubble-trail")).Dispose();bubbles.Present(.81);
        Check(bubbles.LiveCount==0&&!bubbles.Visible,"bubble trail fully fades and stops drawing");
        game.Juice.Judgment(RhythmJudgment.Miss,1,0);Check(game.Juice.CatchBursts.Count==0,"empty Bite still has no successful fish or blood burst");
        foreach(var particles in game.Environment.FindChildren("*","CPUParticles2D",true,false).OfType<CpuParticles2D>())particles.Emitting=false;
        game.Shark.Art.Stop();game.GetNode<Control>("Composition/LeftSignalLane").Visible=false;game.GetNode<Control>("Composition/RightSignalLane").Visible=false;game.Juice.Visible=false;
        var stripe=new ColorRect{Position=new Vector2(500,400),Size=new Vector2(12,120),Color=Colors.Cyan,ZIndex=3};game.AddChild(stripe);
        var block=new ColorRect{Position=new Vector2(100,300),Size=new Vector2(100,100),Color=new Color(.86f,.18f,.58f),ZIndex=5};game.AddChild(block);
        var shader=(ShaderMaterial)game.WorldWaterTreatment.Material;shader.SetShaderParameter("stage_camera_zoom",1);shader.SetShaderParameter("stage_camera_offset",Vector2.Zero);
        using var before=await Capture("hud-neutral");shader.SetShaderParameter("stage_camera_zoom",1.04);shader.SetShaderParameter("stage_camera_offset",new Vector2(12,-8));
        using var after=await Capture("hud-world-camera");bool same=true,moved=false;
        for(int y=310;y<390;y+=5)for(int x=110;x<190;x+=5)same&=before.GetPixel(x,y)==after.GetPixel(x,y);
        for(int y=400;y<520;y+=5)for(int x=480;x<540;x++)moved|=before.GetPixel(x,y)!=after.GetPixel(x,y);
        Check(same&&moved,"rendered pixel test: WORLD moves while later HUD composite stays identical");
        Check(game.WorldWaterTreatment.ZIndex==4&&game.Gauge.ZIndex==5&&game.GetNode<Control>("Composition/JudgmentAnchor").ZIndex==5,"existing WORLD z4/HUD z5 separation preserved");
    }
    private async Task MedalChecks(string save)
    {
        Check(MedalSharkCatalog.RankFor(false,false,false)=="none"&&MedalSharkCatalog.RankFor(true,false,false)=="bronze"&&MedalSharkCatalog.RankFor(true,true,false)=="silver"&&MedalSharkCatalog.RankFor(true,true,true)=="gold","data-driven highest clear / FC / AP medal priority");
        foreach(string rank in new[]{"bronze","silver","gold"})Check(MedalSharkCatalog.Find(rank)!.NaturalFacesLeft,"source medal artwork naturally faces left: "+rank);
        ProgressService.RecordStageResult("song_1",true,false,false);Check(ProgressService.BestStageRank("stage_1")=="bronze","ordinary clear saves Bronze");
        ProgressService.RecordStageResult("song_1",true,true,false);Check(ProgressService.BestStageRank("stage_1")=="silver","Full Combo upgrades Silver");
        ProgressService.RecordStageResult("song_1",true,true,true);ProgressService.RecordStageResult("song_1",true,false,false);Check(ProgressService.BestStageRank("stage_1")=="gold","worse replay never downgrades Gold");
        SaveStore.Initialize(save);ProgressService.Initialize();Check(ProgressService.BestStageRank("stage_1")=="gold"&&ProgressService.HasAllPerfect("song_1"),"highest stage medal survives config reload");
        SaveStore.Write("progress","stage_medal_ranks",new Godot.Collections.Dictionary());SaveStore.Write("progress","full_combo_songs",new[]{"song_2"});SaveStore.Write("progress","all_perfect_songs",new[]{"song_3"});ProgressService.Initialize();
        Check(ProgressService.BestStageRank("stage_2")=="silver"&&ProgressService.BestStageRank("stage_3")=="gold","legacy FC/AP achievements migrate without inventing old clear records");
        var gallery=new Control{Size=new Vector2(1920,1080)};AddChild(gallery);var catalog=GD.Load<StageCatalog>("res://game/lobby/stages.tres");int index=0;
        foreach(string rank in new[]{"none","bronze","silver","gold"})
        {
            var card=GD.Load<PackedScene>("res://game/lobby/StageCard.tscn").Instantiate<StageCard>();gallery.AddChild(card);card.Bind(catalog.Stages[index],true);card.PivotOffset=Vector2.Zero;card.Scale=Vector2.One*.8f;card.Position=new Vector2(30+960*(index%2),15+540*(index/2));
            var swimmer=card.MedalShark;swimmer.SetRank(rank,true);swimmer.SetProcess(false);
            Check(swimmer.Visible==(rank!="none"),"only highest rank shown: "+rank);
            if(rank!="none")
            {
                var def=MedalSharkCatalog.Find(rank)!;swimmer.Present(2);var left=swimmer.Position;Check(swimmer.Art.FlipH,"rightward swimming faces right: "+rank);
                swimmer.Present(3);Check(swimmer.Position.X>left.X,"medal actually swims within card: "+rank);
                swimmer.Present(20);Check(!swimmer.Art.FlipH,"leftward swimming faces left: "+rank);
                Check(Math.Abs(swimmer.Art.Scale.X*def.Region.Size.X/card.Size.X-.16)<.001,"visible medal width is 16% of card: "+rank);
                swimmer.SetRank(rank,false);Check(!swimmer.Visible,"locked stage hides medal: "+rank);swimmer.SetRank(rank,true);swimmer.Present(9);
                string asset=rank=="bronze"?"상어 동.png":rank=="silver"?"상어 은.png":"상어 금.png";Check(swimmer.Art.Texture.ResourcePath=="res://assets/"+asset,"exact supplied medal texture: "+rank);
            }
            if(index==3)Check(card.CompositeCover.GetChildren().OfType<Control>().Count(c=>c.ClipContents)==3,"EX medal preserves three-way environment composite");index++;
        }
        (await Capture("medal-ranks")).Dispose();gallery.QueueFree();await Frames(6);
    }
    private async Task HeavyChecks()
    {
        double nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),orphans=Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        var samples=new List<double>();double last=Time.GetTicksUsec()/1000d,maxDrift=0;int peakBubbles=0,maxUnderruns=0;
        for(int run=0;run<2;run++)
        {
            var game=Spawn("song_4");await Frames(30);
            for(int frame=0;frame<1800;frame++)
            {
                double t=game.Rhythm.SongTimeSeconds;
                if(frame%36==0){game.Juice.Bite(t);game.Shark.Bite(t);game.Juice.Judgment(RhythmJudgment.Perfect,t,frame+1);game.Presentation.ObserveJudgment(RhythmJudgment.Perfect,t);}
                if(frame%180==0){foreach(string type in new[]{"ship_horn","fishing_float","sardine","whale"})game.Interference.Trigger(type,t);game.Signals.LeftLane.Activate(4,WaveKind.Large,2,t);game.Signals.RightLane.Activate(3,WaveKind.Large,2,t);}
                peakBubbles=Math.Max(peakBubbles,game.Juice.JumpBubbles.LiveCount);maxUnderruns=Math.Max(maxUnderruns,game.AudioCues.UnderrunResyncs);
                double heard=game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();if(t>.3&&game.Rhythm.IsClockAdvancing)maxDrift=Math.Max(maxDrift,Math.Abs(t-heard));
                await Frame();double now=Time.GetTicksUsec()/1000d;if(frame>30)samples.Add(now-last);last=now;
                
            }
            if(run==0){game.SuspendForSettings();double t=game.Rhythm.SongTimeSeconds;game.Interference.Trigger("sardine",t);game.Interference.Trigger("whale",t);game.Signals.LeftLane.Activate(4,WaveKind.Large,2,t);game.Juice.Bite(t);game.Juice.Judgment(RhythmJudgment.Perfect,t,1);(await Capture("heavy-ex")).Dispose();}
            game.StopGameplay();game.QueueFree();await Frames(10);
            Check(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)==nodes&&Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)==orphans,"heavy EX retry returns effects/audio/nodes to baseline "+run);
        }
        samples.Sort();GD.Print($"PROFILE stage FX heavy frames={samples.Count} p50={samples[samples.Count/2]:F3}ms p95={samples[(int)(samples.Count*.95)]:F3}ms p99={samples[(int)(samples.Count*.99)]:F3}ms max={samples[^1]:F3}ms peakBubbles={peakBubbles} SFXunderruns={maxUnderruns} clockDrift={maxDrift*1000:F3}ms");
        Check(maxUnderruns==0&&maxDrift<.05,"heavy FX preserve audio scheduling and authoritative sync");
    }
}
