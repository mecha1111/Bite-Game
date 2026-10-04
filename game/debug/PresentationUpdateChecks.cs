using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Startup;
using Gamejam2.Data;
using Gamejam2.Save;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class PresentationUpdateChecks : Node
{
    private int _checks;
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);_checks++;GD.Print("PASS "+text);}
    private async Task Wait(double sec){GetWindow().GrabFocus();await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);}
    private async Task Capture(string name)
    {
        await Wait(.08);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();image.SavePng("res://artifacts/presentation-update/"+name+".png");
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            await GeneratedMapTestFixture.Ensure(this);
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-presentation-update.cfg";AddChild(router);router.GoToLobby();await Wait(.3);
            var lobby=router.ScreenHost!.GetChild<LobbyScreen>(0);
            Check(lobby.Catalog.Stages.Count==4,"main category preserves four standard stages");
            lobby.GetNode<Button>("Categories/Custom").EmitSignal(Button.SignalName.Pressed);await Wait(.6);
            Check(lobby.CustomCategory&&lobby.Catalog.Stages.Count==CustomMapRegistry.Maps.Count,"custom category dynamically lists generated maps");
            foreach(var map in CustomMapRegistry.Maps)
            {
                using var audio=Gamejam2.Audio.SongAudio.Load(map.AudioPath);
                GD.Print($"CUSTOM AUDIO {map.SongId} actual={audio.GetLength():F6} firstPassBpm={map.MusicBpm} available={map.Available}");
                Check(Math.Abs(audio.GetLength()-map.Duration)<1,"provided custom MP3 duration "+map.SongId);
                Check(map.Generated&&map.AudioPath.StartsWith("user://custom_maps/")&&(map.CoverPath.Length==0||map.CoverPath.StartsWith("user://custom_maps/")),"isolated custom assets "+map.SongId);
                if(!map.Available)Check(map.ChartPath==""&&!lobby.Catalog.Stages.Single(s=>s.SongId==map.SongId).IsAvailable,"missing supplied chart does not silently generate/fallback "+map.SongId);
                lobby.Navigate(lobby.Catalog.Stages.ToList().FindIndex(s=>s.SongId==map.SongId)-lobby.SelectedIndex);await Wait(.6);await Capture(map.SongId+"-cover");
            }
            foreach(var size in new[]{new Vector2I(1920,1080),new(1600,900),new(1366,768),new(1280,720),new(2560,1440)})
            {
                GetWindow().Size=size;await Wait(.1);await Capture("custom-menu-"+size.X);
                lobby.GetNode<Button>("Categories/Main").EmitSignal(Button.SignalName.Pressed);await Wait(.1);await Capture("main-menu-"+size.X);
                lobby.GetNode<Button>("Categories/Custom").EmitSignal(Button.SignalName.Pressed);await Wait(.1);
            }
            lobby.GetNode<Button>("Categories/Main").EmitSignal(Button.SignalName.Pressed);router.GoToGameplay("stage_1");await Wait(.2);
            var game=router.ScreenHost.GetChild<GameplayScreen>(0);game.SuspendForSettings();game.SetProcess(false);game.Rhythm.SetProcess(false);
            var indicators=game.GetNode<InterferenceIndicators>("InterferenceIndicators");
            double clock=game.Rhythm.SongTimeSeconds,target=game.Data.Phrases[0].TargetSeconds;
            var template=GD.Load<PackedScene>("res://game/gameplay/WaveLarge.tscn").Instantiate<WavePulse>();game.AddChild(template);template.GlobalPosition=new Vector2(260,540);template.Begin(WaveKind.Large,10);template.Present(11);
            Vector2 origin=template.Origin;float radius=template.CurrentRadius;
            var vortex=game.Interference.GetEffect("sardine").Vortex!;vortex.Begin(10);vortex.Present(11);vortex.Apply(template);
            Check(template.Origin==origin&&template.CurrentRadius==radius&&game.Data.Phrases[0].TargetSeconds==target&&game.Rhythm.SongTimeSeconds==clock,"vortex preserves origin/radius/target/Song Clock");
            Check(template.Art.Position.Y<0&&template.Art.Position.X>0,"visual signal pulled toward upper-middle");
            vortex.Present(10.25);Check(Math.Abs(vortex.Strength-.5)<.001,"half-second smooth vortex formation");vortex.Present(14.75);Check(Math.Abs(vortex.Strength-.5)<.001,"half-second smooth vortex exit");vortex.Present(15);vortex.Apply(template);Check(!vortex.Visible&&template.Art.Position==Vector2.Zero,"five-second vortex restores normal presentation");
            foreach(string type in new[]{"ship_horn","fishing_float","whale","sardine"})game.Interference.Trigger(type,10);
            game.Interference.Present(11);indicators.Present(11,false);
            Check(indicators.Layer==1,"speech bubbles own fixed screen CanvasLayer");
            var bubbles=indicators.GetNode<Control>("SafeArea").GetChildren().OfType<InterferenceBubble>().ToArray();
            Check(indicators.FindChildren("*","Label",true,false).Count==0,"interference presentation is graphic-only");
            Check(bubbles.All(b=>b.Size.Y==192&&Math.Abs(b.Animation.Scale.Y*107-192)<.01),"shared 192px visible height with cropped aspect-preserving frames");
            Check(indicators.FindChildren("*","Control",true,false).OfType<Control>().All(c=>c.MouseFilter==Control.MouseFilterEnum.Ignore),"all indicator controls ignore mouse input");
            Check(bubbles.Single(b=>b.EffectType=="fishing_float").Animation.FlipH&&bubbles.Single(b=>b.EffectType=="fishing_float").Still.FlipH&&!bubbles.Single(b=>b.EffectType=="ship_horn").Animation.FlipH,"float mirrors entire animation; horn retains source orientation");
            Check(bubbles.All(b=>b.Visible),"all four interference indicators present with distinct safe slots");
            Check(bubbles.Single(b=>b.EffectType=="ship_horn").GetGlobalRect().Intersects(bubbles.Single(b=>b.EffectType=="fishing_float").GetGlobalRect())==false,"top indicators never collide");
            foreach(var b in bubbles)Check(b.Animation.SpriteFrames.GetFrameCount("active")>=10,"provided animated frames "+b.EffectType);
            foreach(var size in new[]{new Vector2I(1920,1080),new(1600,900),new(1366,768),new(1280,720),new(2560,1440)})
            {
                GetWindow().Size=size;await Wait(.12);indicators.Present(11.24,false);game.Gauge.Present(1050,1100,800,11);game.Combo.UpdateCombo(12,11,Gamejam2.Rhythm.RhythmJudgment.Perfect);await Capture("hud-current-"+size.X);
                foreach(var b in bubbles)Check(!b.GetGlobalRect().Intersects(game.Gauge.GetGlobalRect())&&!b.GetGlobalRect().Intersects(game.SettingsButton.GetGlobalRect()),"indicator HUD-safe "+b.EffectType+" "+size.X);
                indicators.Present(11,true);Check(bubbles.All(b=>!b.Visible),"menus suppress indicator layer "+size.X);
                game.Result.ShowResult(new GameplayResult("Hear the Tide",1050,800,20,7,6,0,21,33));await Wait(2.1);Check(game.Result.IsRevealed&&game.Result.Gauge.Percentage.Text=="105%"&&game.Result.Gauge.SharkImage.Size.X>400&&game.Result.Gauge.SharkImage.Size.Y>200,"receipt animation and visible shark settle "+size.X);await Capture("result-"+size.X);game.Result.HideResult();
                game.GameOver.ShowStarvation();await Wait(1.2);await Capture("gameover-"+size.X);game.GameOver.HideScreen();
                game.StandaloneSettings.Open();await Wait(.2);await Capture("settings-"+size.X);game.StandaloneSettings.CloseImmediately();game.Countdown.Cancel();
            }
            // Environment-only fixtures: no invented custom chart or substitute song playback.
            GetWindow().Size=new Vector2I(1920,1080);await Wait(.1);
            game.Gauge.ResetDisplay(game.Satiety.Value);game.Gauge.Present(game.Satiety.Value,game.Data.MaxSatiety,game.Data.ClearThreshold);
            foreach(string type in new[]{"whale","fishing_float","ship_horn","sardine"})
            {
                game.Interference.Initialize("song_1",Array.Empty<(string,double)>());indicators.Initialize(game);
                game.Interference.Trigger(type,10);game.Interference.Present(10.26);indicators.Present(10.26,false);
                Check(bubbles.Count(b=>b.Visible)==1&&bubbles.Single(b=>b.Visible).EffectType==type,"independent graphic-only event "+type);
                await Capture(type+"-graphic-only");
                if(type is "fishing_float" or "ship_horn")
                {
                    double emission=type=="fishing_float"?13:11;
                    game.Interference.Present(emission+.01);indicators.Present(emission+.01,false);
                    Check(bubbles.Single(b=>b.EffectType==type).Scale.X>1&&bubbles.Single(b=>b.EffectType==type).Scale.X<=1.081f&&indicators.GetNode<Control>("SafeArea").GetChildCount()==4,"repeat gently pulses same instance, no stacking "+type);
                }
                if(type=="sardine")
                {
                    game.Interference.Present(14.99);indicators.Present(14.99,false);Check(bubbles.Single(b=>b.EffectType==type).Visible,"vortex indicator stays for full five seconds");
                    game.Interference.Present(15);indicators.Present(15,false);indicators.Present(15.2,false);Check(bubbles.All(b=>!b.Visible),"indicator exit fades then hides without blocking input");
                }
            }
            foreach(var b in bubbles)GD.Print($"INDICATOR FINAL {b.EffectType} zone={indicators.SlotFor(b.EffectType)} rect={new Rect2(b.Position,b.Size)}");
            template.Hide();indicators.Present(11,true);game.Interference.Initialize("song_1",Array.Empty<(string,double)>());
            GetWindow().Size=new Vector2I(1920,1080);await Wait(.1);
            foreach(var map in CustomMapRegistry.Maps)
            {
                game.Environment.Apply(map.SongId);game.Environment.Present(0,true);
                Check(game.Environment.Background.Texture!=null&&game.Environment.Background.SelfModulate!=Colors.Black,"generated map inherits its environment profile "+map.SongId);
                await Capture(map.SongId+"-environment-fixture");
            }
            // Development-only simultaneous family gallery at normalized final size.
            game.Interference.Initialize("song_1",Array.Empty<(string,double)>());indicators.Initialize(game);
            var gallery=new CanvasLayer{Layer=10};AddChild(gallery);var backing=new ColorRect{Color=new Color(.015f,.035f,.07f),Size=new Vector2(1920,1080),MouseFilter=Control.MouseFilterEnum.Ignore};gallery.AddChild(backing);
            int column=0;foreach(var b in bubbles){b.Reparent(gallery);b.Position=new Vector2(250+column++*400,440);b.Pop(0);b.Present(1.24,true,false);}
            await Capture("indicator-family-gallery");gallery.QueueFree();
            template.QueueFree();game.StopGameplay();router.QueueFree();await Wait(.2);
            var tutorial=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<Gamejam2.Tutorial.TutorialGameplayScreen>();AddChild(tutorial);await Wait(.1);tutorial.SuspendForSettings();tutorial.SetProcess(false);tutorial.Rhythm.SetProcess(false);
            var tutorialIndicators=tutorial.GetNode<InterferenceIndicators>("InterferenceIndicators");foreach(string type in new[]{"ship_horn","fishing_float","whale","sardine"})tutorial.Interference.Trigger(type,10);tutorial.Interference.Present(11.24);
            foreach(var size in new[]{new Vector2I(1920,1080),new(1600,900),new(1366,768),new(1280,720),new(2560,1440)}){GetWindow().Size=size;await Wait(.1);tutorialIndicators.Present(11.24,false);await Capture("tutorial-"+size.X);var message=tutorial.GetNode<Control>("TutorialHud/MessageArea");foreach(var bubble in tutorialIndicators.GetNode<Control>("SafeArea").GetChildren().OfType<InterferenceBubble>())Check(bubble.Visible&&!bubble.GetGlobalRect().Intersects(message.GetGlobalRect()),"tutorial message remains safe "+bubble.EffectType+" "+size.X);}
            tutorial.StopGameplay();tutorial.QueueFree();await Wait(.1);
            CustomMapRegistry.Reload();Check(CustomMapRegistry.Maps.All(m=>m.Generated),"registry discovers only player-created maps");
            foreach(string id in new[]{"song_1","song_2","song_3","song_4"})Check(GameplayData.Load(id).Phrases.Count>0,"main chart loads independently of local maps "+id);
            GD.Print("PRESENTATION UPDATE VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
