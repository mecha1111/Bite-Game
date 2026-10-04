using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Startup;
using Gamejam2.Save;
namespace Gamejam2.Debugging;
/// <summary>Developer-only isolated receipt/starvation checks, not normal player flow.</summary>
public partial class ResultPresentationChecks : Node
{
    private int _checks;
    private GameplayScreen _game=null!;
    private SceneRouter _router=null!;
    private void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> condition,double timeout=8)
    {ulong end=Time.GetTicksMsec()+(ulong)(timeout*1000);while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException("result check timed out");if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
    private void Keyboard(Key key){foreach(bool pressed in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=pressed});Input.FlushBufferedEvents();}}
    private void Click(Button button)
    {
        var pos=button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion{Position=pos,GlobalPosition=pos},true);
        foreach(bool pressed in new[]{true,false})GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=pos,GlobalPosition=pos,Pressed=pressed},true);
    }
    private async Task Capture(string name) {await Wait(.05);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-receipt-"+name+".png");}
    private void Satiety(double value)=>typeof(SatietyState).GetProperty(nameof(SatietyState.Value))!.SetValue(_game.Satiety,value);
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            _router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();
            _router.SavePath="/private/tmp/bite-receipt-check.cfg";
            if(FileAccess.FileExists(_router.SavePath))DirAccess.RemoveAbsolute(_router.SavePath);
            AddChild(_router);_router.GoToLobby();_router.GoToGameplay("stage_1");GetWindow().GrabFocus();
            _game=_router.ScreenHost!.GetChild<GameplayScreen>(0);_game.SuspendForSettings();
            var r=_game.Result;
            r.ShowResult(new GameplayResult("Hear the Tide",880,800,20,7,6,0,21,33));
            Check(r.RetryButton.Disabled && r.Gauge.DisplayedSatiety==0,"receipt begins empty / actions blocked");
            await Until(()=>r.IsRevealed,4);
            Check(r.Perfect.Text=="20"&&r.Good.Text=="7"&&r.Bad.Text=="6"&&r.Miss.Text=="0","20/7/6/0 numeric Label counts");
            Check(r.MaxCombo.Text=="21"&&r.Gauge.Percentage.Text=="88%"&&r.Headline.Text=="CLEAR","21 combo / 880=88% / CLEAR");
            Check(Math.Abs(((ShaderMaterial)r.Gauge.SharkImage.Material).GetShaderParameter("fill_ratio").AsDouble()-.88)<.0001,"gauge shader fill .88, not 880/1100");
            foreach(string key in new[]{"Perfect","Good","Bad","Miss"})
            {
                var image=r.Rows.GetNode<TextureRect>(key+"Row/"+key+"Image");var atlas=(AtlasTexture)image.Texture;
                Check(atlas.Atlas.ResourcePath==$"res://assets/{key.ToUpperInvariant()}.png"&&image.Size.Y==48,"judgment image normalized "+key);
            }
            Check(r.Gauge.SharkImage.Size.X>400&&r.Gauge.SharkImage.Size.Y>200&&r.Gauge.Percentage.Size.X>400,"result shark and percent retain nonzero anchored layout");
            Check(r.RetryButton.HasFocus(),"receipt Retry default focus");await Capture("clear-880");
            foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1600,900),new Vector2I(1366,768),new Vector2I(1280,720),new Vector2I(2560,1440)})
            {
                GetWindow().Size=size;await Wait(.12);
                var transform=GetViewport().GetFinalTransform();double scale=Math.Min(size.X/1920.0,size.Y/1080.0);
                Check(Math.Abs(transform.X.Length()-scale)<.002&&Math.Abs(transform.Y.Length()-scale)<.002,"receipt uniform scaling "+size);
                Check(r.Panel.GetGlobalRect().GetCenter().DistanceTo(new Vector2(960,505))<1&&r.Panel.Size.X==1320,"two-column board 68.75% width "+size);
                await Capture($"result-{size.X}x{size.Y}");
            }
            GetWindow().Size=new Vector2I(1280,720);await Wait(.05);
            var content=r.Panel.GetNode<Control>("Content");
            Check(r.Rows.GetGlobalRect().End.X<r.Gauge.GetGlobalRect().Position.X,"judgments left / dominant shark summary right");
            Check(r.MaxCombo.GetGlobalRect().Position.Y>r.Rows.GetGlobalRect().End.Y,"max combo below judgment rows");
            Check(r.Actions.GetGlobalRect().Position.Y>r.Panel.GetGlobalRect().End.Y,"compact actions below board");
            Check(r.Gauge.Percentage.GetGlobalRect().Position.Y>=r.Gauge.SharkImage.GetGlobalRect().End.Y,"percentage below shark gauge");
            Check(content.GetChildren().OfType<Control>().Max(c=>c.Position.Y+c.Size.Y)<=content.Size.Y+1,"receipt fits height without overflow");
            r.ShowResult(new GameplayResult("Hear the Tide",799,800,1,1,1,1,2,4));r.Reveal();
            Check(r.Headline.Text=="FAILED"&&r.Gauge.Percentage.Text=="79.9%","below 800 FAILED / percentage does not round to clear");await Capture("failed-799");
            foreach(double value in new[]{0.0,500,800,1000,1100})
            {r.Gauge.Present(value);Check(r.Gauge.Percentage.Text==$"{value/10:0}%","satiety percentage "+value);}
            r.ShowResult(new GameplayResult("Hear the Tide",880,800,8,0,0,0,8,8));r.Reveal();
            Check(r.Achievement.Text=="ALL PERFECT"&&r.AchievementShark.Visible,"AP supersedes FC / gold shark accent");await Capture("all-perfect");
            r.ShowResult(new GameplayResult("Hear the Tide",880,800,7,1,0,0,8,8));r.Reveal();
            Check(r.Achievement.Text=="FULL COMBO","FC silver accent");
            ProgressService.RecordSongAchievements("fixture",true,false);SaveStore.Initialize(_router.SavePath);ProgressService.Initialize();
            Check(ProgressService.HasFullCombo("fixture")&&!ProgressService.HasAllPerfect("fixture"),"FC saved and reloaded per song");
            ProgressService.RecordSongAchievements("fixture",true,true);ProgressService.RecordSongAchievements("fixture",false,false);SaveStore.Initialize(_router.SavePath);ProgressService.Initialize();
            Check(ProgressService.HasFullCombo("fixture")&&ProgressService.HasAllPerfect("fixture"),"AP persisted / weaker later run preserves best");
            _game.Restart();
            foreach(var phrase in _game.Data.Phrases)
            {
                await Until(()=>_game.Rhythm.SongTimeSeconds>=phrase.TargetSeconds,30);
                _game.Rhythm.TryJudgeInput();
            }
            await Until(()=>_game.Result.Visible,30);
            Check(ProgressService.HasAllPerfect("song_1")&&_game.Result.Visible&&!_game.GameOver.Visible,"actual final-result path stores achievement by song ID");
            Keyboard(Key.Space);Check(_game.Result.IsRevealed,"Space skips receipt reveal without activating behind it");
            Keyboard(Key.Space);await Wait(.05);Check(_game.Rhythm.IsRunning&&!_game.Result.Visible,"Space activates Retry through shared Button signal");
            await Until(()=>_game.Rhythm.SongTimeSeconds>.2);
            Satiety(0);await Until(()=>_game.GameOver.Visible);
            Check(!_game.Result.Visible&&_game.InputBlocked&&!_game.Music.Playing&&!_game.Rhythm.IsRunning,"starvation stops gameplay, no final receipt first");
            double frozen=_game.Rhythm.SongTimeSeconds;int bites=_game.Shark.BiteCount;
            Input.ParseInputEvent(new InputEventAction{Action="rhythm_input",Pressed=true});Input.FlushBufferedEvents();
            Input.ParseInputEvent(new InputEventAction{Action="rhythm_input",Pressed=false});Input.FlushBufferedEvents();
            await Wait(1.1);
            Check(_game.Rhythm.SongTimeSeconds==frozen&&_game.Satiety.Value==0&&_game.Shark.BiteCount==bites,"game over clock/satiety/input frozen");
            Check(_game.Shark.Position.Y==0&&_game.Shark.Art.SelfModulate.A==0&&_game.Shark.Skeleton.Visible&&_game.Shark.Skeleton.Modulate.A==1,"normal shark fades into world skeleton");
            Check(_game.Signals.LeftLane.Modulate.A==0&&_game.Signals.RightLane.Modulate.A==0,"active signals fade without advancing chart");
            Check(_game.GameOver.Gauge.Percentage.Text=="0%"&&_game.GameOver.Actions.RetryButton.HasFocus(),"empty gauge / Game Over Retry focus");await Capture("game-over");
            Click(_game.GameOver.Actions.RetryButton);await Wait(.05);
            Check(!_game.GameOver.Visible&&_game.Rhythm.IsRunning,"mouse Retry uses shared native Button");
            Satiety(0);await Until(()=>_game.GameOver.Visible);_game.GameOver.Reveal();
            Keyboard(Key.Enter);await Wait(.05);
            Check(!_game.GameOver.Visible&&!_game.Result.Visible&&_game.Rhythm.IsRunning&&_game.Shark.Position.Y==0,"Enter retry resets starvation visuals and song");
            Satiety(0);await Until(()=>_game.GameOver.Visible);_game.GameOver.Reveal();Keyboard(Key.Right);
            Check(_game.GameOver.Actions.SongSelectButton.HasFocus(),"Game Over keyboard Right focus");Keyboard(Key.Enter);
            Check(_router.ScreenHost.GetChild(0) is Gamejam2.Lobby.LobbyScreen,"Game Over song select returns Lobby");
            _router.GoToGameplay("stage_1");_game=_router.ScreenHost.GetChild<GameplayScreen>(0);Satiety(0);await Until(()=>_game.GameOver.Visible);Keyboard(Key.Escape);
            Check(_router.ScreenHost.GetChild(0) is Gamejam2.Lobby.LobbyScreen,"Game Over Escape shares song-select route");
            _router.QueueFree();await Wait(.2);
            GD.Print($"RESULT PRESENTATION VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
