using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Only the requested short v3 smoke checks; no full-song playback or chart rewriting.</summary>
public partial class IntegratedV3Checks : Node
{
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);}
    private async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static void Seek(RhythmController rhythm,double time)
    {var f=BindingFlags.Instance|BindingFlags.NonPublic;typeof(RhythmController).GetField("_clockBaseSeconds",f)!.SetValue(rhythm,time);typeof(RhythmController).GetField("_startTimeUsec",f)!.SetValue(rhythm,Time.GetTicksUsec());typeof(RhythmController).GetField("_audioStartDelaySeconds",f)!.SetValue(rhythm,0d);}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Capture(string name)
    {await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/v3/"+name+".png");}
    private GameplayScreen Spawn(string song)
    {var g=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();g.SongId=song;AddChild(g);return g;}
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-v3-smoke.cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;
            var main=Spawn("song_1");await Frames();
            Check(main.RunState==GameplayScreen.GameplayRunState.Playing&&main.Data.ChartPath.EndsWith("stage1_hear_the_tide_chart_v3.csv")&&main.Data.Phrases.Count==57,"A Main Stage loads supplied v3 chart");
            var first=main.Data.Phrases[0];double deadline=Time.GetTicksMsec()+6000;
            while(main.Rhythm.SongTimeSeconds<first.TargetSeconds&&Time.GetTicksMsec()<deadline)await Frames(1);
            main._Input(new InputEventAction{Action="rhythm_input",Pressed=true});
            Check(main.Rhythm.LastResult is {Judgment:RhythmJudgment.Perfect},"B first musical target performed at original v3 timestamp during actual audio playback");
            long score=main.Satiety.Score;var second=main.Data.Phrases[1];Seek(main.Rhythm,second.TargetSeconds+.17);main._Process(0);main._Input(new InputEventAction{Action="rhythm_input",Pressed=true});
            Check(main.Rhythm.LastResult is {Judgment:RhythmJudgment.Bad}&&main.Satiety.Score==score,"D forced BAD awards zero score; Satiety remains separate");
            double now=main.Rhythm.SongTimeSeconds;main.Interference.Trigger("ship_horn",now);main.Interference.Present(now+.08);
            var wave=main.Interference.GetEffect("ship_horn").ActivePulses[0];
            Check(wave.FullyOpaque&&((ShaderMaterial)wave.Art.Material).GetShaderParameter("opacity").AsSingle()==1&&main.Interference.Effects.Modulate.A==1,"E spawned interference ring is fully opaque");
            var fx=main.Presentation.Events.First(e=>e.Type=="FOLLOW_WAVE");
            var hud=new Control[]{main.Gauge,main.Combo,main.Feedback.GetParent<Control>(),main.SettingsButton};var rectangles=hud.Select(c=>c.GetGlobalRect()).ToArray();
            main.Presentation.Present(fx.Time+fx.Duration*.5);
            Check(main.Presentation.CameraOffset.Length()>0&&hud.Select((c,i)=>c.GetGlobalRect()==rectangles[i]).All(x=>x),"F FOLLOW_WAVE moves WORLD shader framing while HUD stays fixed");
            main.Combo.UpdateCombo(27,main.Rhythm.SongTimeSeconds,RhythmJudgment.Perfect);main.Combo.Present(main.Rhythm.SongTimeSeconds+.05);
            Check(main.Combo.Visible&&main.Combo.Number.Text=="27"&&main.Combo.Number.GetThemeFontSize("font_size")==96&&main.Combo.GetNode<Label>("Caption").Text=="COMBO","H redesigned large combo number / small label");
            await Capture("main-combo-interference");main.StopGameplay();main.QueueFree();await Frames();
            var custom=Spawn("custom_under_the_sea");await Frames();
            Check(custom.RunState==GameplayScreen.GameplayRunState.Playing&&custom.Data.Phrases.Count==137&&custom.Data.ChartPath.EndsWith("custom_under_the_sea_chart_v3.csv"),"C Under the Sea loads dense v3 chart in normal Gameplay");
            await Capture("under-the-sea");custom.StopGameplay();custom.QueueFree();await Frames();
            var lobby=GD.Load<PackedScene>("res://game/lobby/Lobby.tscn").Instantiate<LobbyScreen>();AddChild(lobby);await Frames();
            lobby._Input(new InputEventKey{PhysicalKeycode=Key.E,Pressed=true});Check(lobby.CustomCategory,"G E switches to Custom");
            await ToSignal(GetTree().CreateTimer(.30),SceneTreeTimer.SignalName.Timeout);await Capture("custom-category");
            lobby._Input(new InputEventKey{PhysicalKeycode=Key.Q,Pressed=true});Check(!lobby.CustomCategory,"G Q switches to Main");
            GD.Print("REQUESTED V3 SMOKE CHECKS PASSED; musical taste is not certified by automated timing checks.");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
