using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Lobby;
using Gamejam2.Gameplay;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Exercises the actual Startup router, not standalone Gameplay. Isolated developer save.</summary>
public partial class RuntimeWiringAudit : Node
{
    private SceneRouter _router=null!;
    private GameplayScreen? _game;
    private int _checks;
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);_checks++;}
    private async Task At(double t)
    {
        ulong deadline=Time.GetTicksMsec()+12000;
        while(_game!.Rhythm.SongTimeSeconds<t)
        {
            if(Time.GetTicksMsec()>deadline)throw new TimeoutException($"clock did not reach {t}: {_game.Rhythm.PlaybackState}, blocked={_game.InputBlocked}, manual={_game.Rhythm.IsManuallyPaused}, focus={_game.Rhythm.IsFocusPaused}");
            if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }
    private async Task Wait(double sec)=>await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
    public override void _Process(double delta){if(_game?.Rhythm.IsFocusPaused==true)GetWindow().GrabFocus();}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Capture(string file){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-runtime-"+file+".png");}
    private void Space(){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});Input.FlushBufferedEvents();Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=false});Input.FlushBufferedEvents();}
    private async Task Run()
    {
        try
        {
            if(GetTree().CurrentScene is SceneRouter main)_router=main;
            else {_router=GD.Load<PackedScene>((string)ProjectSettings.GetSetting("application/run/main_scene")).Instantiate<SceneRouter>();_router.SavePath="/private/tmp/bite-runtime-audit-after-"+OS.GetProcessId()+".cfg";AddChild(_router);}
            GD.Print($"AUDIT ActualMainScene={GetTree().CurrentScene.SceneFilePath} Startup={_router.GetPath()}");GetWindow().GrabFocus();
            Check(Math.Abs(SettingsService.MasterVolume-.5)<.001,"fresh Master defaults to 50%");
            Gamejam2.Save.SaveStore.Write("settings","master_volume",.73);SettingsService.Initialize();Check(Math.Abs(SettingsService.MasterVolume-.73)<.001,"saved Master preference survives initialization");SettingsService.SetMasterVolume(.5);
            Gamejam2.Save.SaveStore.Write("progress","tutorial_completed",true);Gamejam2.Save.SaveStore.Flush();SettingsService.CompleteCalibration();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);
            await Wait(1);var title=_router.ScreenHost!.GetChildren().OfType<TitleScreen>().Single();
            var art=title.GetNode<Sprite2D>("Title");var material=(ShaderMaterial)art.Material;
            GD.Print($"AUDIT Title={title.SceneFilePath} node={art.GetPath()} texture={art.Texture.ResourcePath} material={material.ResourcePath} shader={material.Shader.ResourcePath}");Capture("title-final");
            var titleRef=material.GetShaderParameter("refraction_strength");var titleCaustic=material.GetShaderParameter("caustic_strength");material.SetShaderParameter("refraction_strength",titleRef.AsSingle()*2);material.SetShaderParameter("caustic_strength",titleCaustic.AsSingle()*2);Capture("title-debug0");await Wait(4);Capture("title-debug4");material.SetShaderParameter("refraction_strength",titleRef);material.SetShaderParameter("caustic_strength",titleCaustic);
            title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(1);
            var lobby=_router.ScreenHost.GetChildren().OfType<LobbyScreen>().Single();GD.Print($"AUDIT Lobby={lobby.SceneFilePath} preview={lobby.GetNode<SongPreview>("SongPreview").PlayerA.Stream.ResourcePath}");
            lobby.SelectCurrent();await Wait(.5);_game=_router.ScreenHost.GetChildren().OfType<GameplayScreen>().Single();
            var world=(ShaderMaterial)_game.WorldWaterTreatment.Material;
            GD.Print($"AUDIT Gameplay={_game.SceneFilePath} CurrentSongId={_game.SongId} Audio={_game.Music.Stream.ResourcePath} length={_game.Music.Stream.GetLength():F6} position={_game.Music.GetPlaybackPosition():F6} Chart={_game.Data.ChartPath}");
            GD.Print($"AUDIT Shader owner={_game.WorldWaterTreatment.GetPath()} visible={_game.WorldWaterTreatment.IsVisibleInTree()} size={_game.WorldWaterTreatment.Size} material={world.ResourcePath} shader={world.Shader.ResourcePath}");
            GD.Print($"AUDIT Shark={_game.Shark.GetPath()} art={_game.Shark.Art.GetPath()} animation={_game.Shark.Art.Animation} available={string.Join(',',_game.Shark.Art.SpriteFrames.GetAnimationNames())}");
            foreach(var key in InputMap.ActionGetEvents("rhythm_input"))GD.Print($"AUDIT InputMap rhythm_input {key.AsText()}");
            foreach(var p in _game.Data.Phrases.Take(5))GD.Print($"AUDIT Event {p.Index} {p.PatternId} Start={p.StartSeconds:F6} Target={p.TargetSeconds:F6}");
            int before=_game.Shark.BiteCount;Space();await Wait(.16);GD.Print($"AUDIT Space at intro count={_game.Shark.BiteCount-before} frame={_game.Shark.Art.Frame} time={_game.Rhythm.SongTimeSeconds:F3}");
            Check(_game.Shark.BiteCount==before+1&&_game.Shark.Art.Animation=="bite"&&_game.Shark.Art.Frame>0,"physical Space immediately runs visible bite frames during intro");Capture("bite-intro");await Wait(.6);Check(_game.Shark.Art.Animation=="idle","bite completes then returns to idle");
            Capture("gameplay");_game.SuspendForSettings();RenderingServer.ForceDraw();var treated=GetViewport().GetTexture().GetImage();_game.WorldWaterTreatment.Visible=false;RenderingServer.ForceDraw();var untreated=GetViewport().GetTexture().GetImage();
            int sharkDiff=0,hudDiff=0;for(int y=835;y<1070;y+=3)for(int x=870;x<1050;x+=3)if(!treated.GetPixel(x,y).IsEqualApprox(untreated.GetPixel(x,y)))sharkDiff++;
            for(int y=130;y<146;y+=3)for(int x=650;x<1250;x+=3)if(!treated.GetPixel(x,y).IsEqualApprox(untreated.GetPixel(x,y)))hudDiff++;
            GD.Print($"AUDIT shader difference shark region={sharkDiff}, HUD region={hudDiff}");treated.Dispose();untreated.Dispose();Check(sharkDiff>1000&&hudDiff==0,"world shader changes shark while opaque HUD track is identical");_game.WorldWaterTreatment.Visible=true;
            var original=new System.Collections.Generic.Dictionary<string,Variant>();foreach(string key in new[]{"refraction_strength","caustic_strength","fog_strength","world_tint","time_seconds"})original[key]=world.GetShaderParameter(key);
            world.SetShaderParameter("refraction_strength",original["refraction_strength"].AsSingle()*2);world.SetShaderParameter("caustic_strength",original["caustic_strength"].AsSingle()*2);world.SetShaderParameter("fog_strength",original["fog_strength"].AsSingle()*2);world.SetShaderParameter("world_tint",new Vector3(.65f,.86f,1));Capture("world-debug0");world.SetShaderParameter("time_seconds",original["time_seconds"].AsDouble()+4);Capture("world-debug4");foreach(var pair in original)world.SetShaderParameter(pair.Key,pair.Value);Capture("world-final");
            _game.BeginResumeCountdown();await Wait(3.4);
            await At(_game.Data.Phrases[0].TargetSeconds+.002);before=_game.Shark.BiteCount;double hitAt=_game.Rhythm.SongTimeSeconds;Space();await At(hitAt+.08);Check(_game.Shark.BiteCount==before+1&&_game.Rhythm.LastResult?.Judgment==Gamejam2.Rhythm.RhythmJudgment.Perfect&&_game.Shark.Art.Frame>0,"accurate physical Space catches first prey and attacks");Capture("bite-perfect");
            await At((_game.Data.Phrases[0].TargetSeconds+_game.Rhythm.AppliedLateCaptureSeconds+_game.Data.Phrases[1].StartSeconds)/2);before=_game.Shark.BiteCount;double whiffAt=_game.Rhythm.SongTimeSeconds;Space();await At(whiffAt+.08);GD.Print($"WHIFF fixture at={whiffAt:F6} after={_game.Rhythm.SongTimeSeconds:F6} bites={_game.Shark.BiteCount-before} empty={_game.Satiety.EmptyBites} frame={_game.Shark.Art.Frame} animation={_game.Shark.Art.Animation} state={_game.PreyState}");Check(_game.Shark.BiteCount==before+1&&_game.Satiety.EmptyBites==2&&_game.Shark.Art.Frame>0,"empty-water MISS still visibly attacks");Capture("bite-whiff");
            await At(_game.Data.Phrases[1].StartSeconds+.2);Space();Check(_game.Rhythm.LastResult?.Judgment==Gamejam2.Rhythm.RhythmJudgment.Miss,"early physical Space consumes second prey as MISS");int perfect=_game.Satiety.CreateResult().Perfect;
            for(int n=0;n<6;n++){await At(_game.Rhythm.SongTimeSeconds+.085);before=_game.Shark.BiteCount;Space();Check(_game.Shark.BiteCount==before+1,"each accepted mash press animates");}
            await At(_game.Data.Phrases[1].TargetSeconds+.002);Space();Check(_game.Satiety.CreateResult().Perfect==perfect,"later accurate Space cannot repair resolved prey");
            _router.Settings!.Open();await Wait(.4);_router.Settings.MasterSlider.GrabFocus();before=_game.Shark.BiteCount;Space();Check(_game.Shark.BiteCount==before,"Settings Space never bites");_router.Settings.Close();await Wait(.3);Space();Check(_game.IsCountingDown&&_game.Shark.BiteCount==before,"countdown Space never bites");await Wait(3.1);
            _game.Result.ShowResult(new("Fixture",880,800,20,7,6,0,21,33));_game.SuspendForSettings();Space();Check(_game.Shark.BiteCount==before,"result UI Space never reaches gameplay bite");_game.Result.HideResult();
            _game.BeginResumeCountdown();await Wait(3.4);typeof(SatietyState).GetProperty("Value")!.SetValue(_game.Satiety,0d);await Wait(.1);before=_game.Shark.BiteCount;Space();Check(_game.RunState==GameplayScreen.GameplayRunState.GameOver&&_game.Shark.BiteCount==before&&!_game.Result.Visible&&_game.ResultPresentationCount==0,"starvation opens Game Over only and blocks gameplay Space");
            _game.StopGameplay();_router.QueueFree();await Wait(.1);GD.Print($"RUNTIME WIRING VERIFIED: {_checks} checks via Title→Lobby→Gameplay and actual physical Space.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());if(_game!=null&&GodotObject.IsInstanceValid(_game))_game.StopGameplay();if(_router!=null&&GodotObject.IsInstanceValid(_router))_router.QueueFree();await Wait(.1);GetTree().Quit(1);}
    }
}
