using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Gameplay;
using Gamejam2.Settings;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
/// <summary>Internal drawer/context verification; isolated save, outside player flow.</summary>
public partial class SettingsContextChecks : Node
{
    private int _checks;
    private SceneRouter _router=null!;
    private GameplayScreen _game=null!;
    private void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> predicate,double seconds=8)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(!predicate())
        {
            if(Time.GetTicksMsec()>end)throw new TimeoutException("settings countdown did not resume");
            if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }
    private void Key(Key key){foreach(bool pressed in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=pressed});Input.FlushBufferedEvents();}}
    private async Task Capture(string name){await Wait(.05);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-settings-context-"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            _router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();_router.SavePath="/private/tmp/bite-settings-context.cfg";
            if(FileAccess.FileExists(_router.SavePath))DirAccess.RemoveAbsolute(_router.SavePath);
            AddChild(_router);GetWindow().GrabFocus();GetWindow().Size=new Vector2I(1920,1080);
            var s=_router.Settings!;s.Open();await Wait(.4);
            Check(!s.SongHeader.Visible&&!s.LeaveButton.Visible,"Title hides song header and gameplay exit");await Capture("title");
            s.CloseImmediately();_router.GoToLobby();_router.GoToGameplay("stage_1");_game=_router.ScreenHost!.GetChild<GameplayScreen>(0);
            await Wait(.2);Key(Godot.Key.Escape);await Wait(.4);
            Check(s.Visible&&_game.InputBlocked&&_game.Rhythm.IsManuallyPaused&&!_game.Music.Playing,"ESC opens drawer and safely pauses gameplay");
            Check(s.SongHeader.Visible&&s.SongTitle.Text==_game.Data.SongName&&s.SongTitle.IsAncestorOf(s.Scroll)==false,"song title is metadata bound");
            Check(s.SongHeader.GetParent().Name=="Header"&&!s.Scroll.IsAncestorOf(s.SongHeader),"song title outside settings list in header");
            string path="Drawer/Margins/Layout/ScrollContainer/SettingsContent/";
            foreach(var pair in new[]{("DisplaySection","화면"),("AudioSection","소리"),("RhythmSection","리듬")})
                Check(s.GetNode<Label>(path+pair.Item1+"/Divider/Title").Text==pair.Item2,"correct divider "+pair.Item2);
            Check(s.GetNode<Label>(path+"AudioSection/Music/Caption").Text=="음악"&&s.GetNode<Label>(path+"AudioSection/Effects/Caption").Text=="효과음","distinct audio labels");
            var content=s.GetNode<Control>(path.TrimEnd('/'));
            Check(s.Scroll.Size.Y>0&&s.ResetProgressButton.IsInsideTree(),"settings retain scrollable bottom data action");
            Check(s.GetNodeOrNull("Drawer/Margins/Layout/Footer/Hint")==null,"ambiguous ESC exit hint removed");
            Check(s.LeaveButton.Visible&&s.CloseButton.Visible,"gameplay exit and close in footer");
            Check(s.InputOffset.Amount.GetThemeConstant("outline_size")==0&&s.RecalibrateButton.GetThemeConstant("outline_size")==0,"clean offset/action typography");
            Check(s.DisplayMode.GetThemeStylebox("focus") is StyleBoxEmpty && s.RecalibrateButton.GetThemeStylebox("focus") is StyleBoxEmpty,"native controls have empty focus StyleBox");
            s.RecalibrateButton.GrabFocus(); Check(s.RecalibrateButton.SelfModulate.G>1 && s.RecalibrateButton.HasFocus(),"focus remains enabled with custom brightness");
            s.DisplayMode.ShowPopup();await Wait(.05);Key(Godot.Key.Escape);await Wait(.05);
            Check(s.Visible&&!s.DisplayMode.GetPopup().Visible,"dropdown Escape closes dropdown before drawer");
            s.MasterSlider.Value=61;s.MusicSlider.Value=71;s.EffectsSlider.Value=23;
            Check(Math.Abs(SettingsService.MasterVolume-.61)<.001&&Math.Abs(SettingsService.MusicVolume-.71)<.001&&Math.Abs(SettingsService.EffectsVolume-.23)<.001,"live volume settings preserved");
            Check(Math.Abs(AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Music"))-.71)<.001,"music bus updates immediately");
            s.InputOffset.Value=83;s.VisualOffset.Value=37;s.InputOffset.Plus.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Math.Abs(SettingsService.InputOffsetSeconds-.084)<.00001&&s.InputOffset.Amount.Text=="+84 ms","compact offset step and persistence binding");Check(Math.Abs(SettingsService.VisualOffsetSeconds-.037)<.00001 && s.VisualOffset.Amount.Text=="+37 ms","independent Visual Offset remains editable/persistent");
            await Capture("gameplay");
            s.LeaveButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.05);
            Check(s.ExitConfirmation.Visible&&s.CancelExitButton.HasFocus(),"confirmation appears with safe Cancel focus");
            Check(s.DisplayMode.FocusMode==Control.FocusModeEnum.None,"confirmation blocks focus behind modal");
            Key(Godot.Key.Tab);Check(s.ConfirmExitButton.HasFocus(),"confirmation Tab stays inside modal");
            Key(Godot.Key.Tab);Check(s.CancelExitButton.HasFocus(),"confirmation Tab cycles to Cancel");await Capture("confirm");
            double paused=_game.Rhythm.SongTimeSeconds;Key(Godot.Key.Escape);await Wait(.05);
            Check(!s.ExitConfirmation.Visible&&s.Visible&&s.LeaveButton.HasFocus()&&!_game.IsCountingDown,"ESC cancels confirmation, keeps run paused");
            s.LeaveButton.EmitSignal(BaseButton.SignalName.Pressed);s.CancelExitButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!s.ExitConfirmation.Visible&&_game.Rhythm.SongTimeSeconds==paused,"Cancel preserves frozen run");
            Key(Godot.Key.Escape);await Wait(.3);
            Check(!s.Visible&&_game.IsCountingDown&&_game.Countdown.CurrentNumber=="3","ESC closes drawer then countdown 3");
            await Until(()=>!_game.IsCountingDown&&_game.Rhythm.SongTimeSeconds>paused);
            Check(!_game.IsCountingDown&&!_game.InputBlocked&&_game.Rhythm.SongTimeSeconds>paused,"normal close resumes clock after countdown");
            _game.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.4);
            s.RecalibrateButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            var calibration=_router.ScreenHost.GetChild<Gamejam2.Calibration.CalibrationScreen>(0);calibration.Flow!.Calibration!.SkipWithDefaults();await Wait(.15);
            Check(_router.ScreenHost.GetChild(0)==_game&&s.Visible&&s.SongTitle.Text==_game.Data.SongName&&s.LeaveButton.Visible,"Calibration returns paused gameplay drawer/context");
            s.CloseImmediately();await Until(()=>!_game.IsCountingDown&&_game.Rhythm.IsClockAdvancing);
            Check(_game.AudioCues.IsCuePlaying,"Calibration return reconnects synchronized wave audio");
            s.Open();await Wait(.4);
            s.LeaveButton.EmitSignal(BaseButton.SignalName.Pressed);Key(Godot.Key.Right);Check(s.ConfirmExitButton.HasFocus(),"keyboard confirmation navigation");
            Key(Godot.Key.Enter);
            Check(!_game.IsCountingDown&&!_game.Rhythm.IsRunning&&!_game.Music.Playing,"abandon path starts no countdown/audio resume");
            await Wait(.05);
            Check(_router.ScreenHost.GetChild(0) is LobbyScreen&&!s.Visible,"confirmed exit returns Song Select, never quits");
            _router.GoToTitle();s.Open();await Wait(.4);
            Check(!s.SongHeader.Visible&&!s.LeaveButton.Visible&&!s.ExitConfirmation.Visible,"gameplay-only context cleared on Title");
            GD.Print($"SETTINGS CONTEXT VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
