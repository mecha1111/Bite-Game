using System;
using System.Linq;
using Godot;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Save;
using Gamejam2.Lobby;
using Gamejam2.Gameplay;
using Gamejam2.Tutorial;
namespace Gamejam2.Startup;
public enum CalibrationReason { FirstLaunch, Settings }
/// <summary>화면 전환/복귀 문맥만 소유한다. 측정과 진행/설정은 각 작은 service에 맡긴다.</summary>
public partial class SceneRouter : Control
{
    [Export] public Control? ScreenHost { get; set; }
    [Export] public RhythmController? Rhythm { get; set; }
    [Export] public AudioStreamPlayer? Music { get; set; }
    [Export] public SettingsPopup? Settings { get; set; }
    /// <summary>검증은 별도 파일로 격리할 수 있다. 정상 실행은 user:// 경로를 사용한다.</summary>
    [Export] public string SavePath { get; set; } = SaveStore.DefaultPath;
    private Control? _screen;
    private CalibrationReason _reason;
    private bool _returnToLobby;
    private int _returnLobbySelection;
    private bool _returnLobbyCustom,_tutorialReturnCustom;
    private bool _settingsPausedPlayback;
    private GameplayScreen? _suspendedGameplay;
    private bool _leavingGameplay;
    private bool _calibrationTransitionPending,_sceneChanging;
    public override void _EnterTree()=>BuildIdentity.LogBoot();
    public override void _Ready()
    {
        if (ScreenHost == null || Rhythm == null || Music == null || Settings == null)
        { GD.PushError("Startup: ScreenHost/Rhythm/Music/Settings를 연결하세요."); return; }
        if(BuildFeatures.DevelopmentEnabled&&OS.GetCmdlineUserArgs().Contains("--runtime-audit-driver"))SavePath="/private/tmp/bite-main-runtime-audit-"+OS.GetProcessId()+".cfg";
        if(BuildFeatures.DevelopmentEnabled&&OS.GetCmdlineUserArgs().Any(arg=>arg is "--v2-audit-driver" or "--musical-audit-driver"))SavePath="/private/tmp/bite-v2-main-audit-"+OS.GetProcessId()+".cfg";
        SaveStore.Initialize(SavePath); ProgressService.Initialize(); SettingsService.Initialize();
        Rhythm.UserOffsetSeconds = SettingsService.InputOffsetSeconds;
        Settings.RecalibrationRequested += CalibrateFromSettings; Settings.VisibilityChanged += UpdateMenuInput; GoToTitle();
        Settings.LeaveGameplayRequested += LeaveGameplay;Settings.TutorialRequested += ReplayTutorial;
        if(BuildFeatures.DevelopmentEnabled&&OS.GetCmdlineUserArgs().Contains("--runtime-audit-driver"))Callable.From(()=>GetTree().Root.AddChild(new Gamejam2.Debugging.RuntimeWiringAudit())).CallDeferred();
        if(BuildFeatures.DevelopmentEnabled&&OS.GetCmdlineUserArgs().Any(arg=>arg is "--v2-audit-driver" or "--musical-audit-driver"))Callable.From(()=>GetTree().Root.AddChild(new Gamejam2.Debugging.V2RuntimeChecks())).CallDeferred();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(BuildFeatures.DevelopmentEnabled&&Settings is {Visible:false}&&(_screen is TitleScreen or LobbyScreen)&&input is InputEventKey {Pressed:true,Echo:false,PhysicalKeycode:Key.T})
        {
            ProgressService.EnableDebugUnlock();var toast=GetNode<Label>("DeveloperToast");toast.Text="[DEBUG] 모든 스테이지 해금";toast.Show();
            _debugToast?.Kill();_debugToast=CreateTween();_debugToast.TweenInterval(1.7);_debugToast.TweenCallback(Callable.From(()=>toast.Hide()));GetViewport().SetInputAsHandled();return;
        }
        // GUI popups get first refusal; calibration keeps its own input flow.
        if (Settings == null || Settings.Visible || _screen is not TitleScreen and not GameplayScreen
            || input.IsEcho() || !input.IsActionPressed("ui_cancel")) return;
        Settings.Open(); GetViewport().SetInputAsHandled();
    }
    private Tween? _debugToast;
    private void UpdateMenuInput()
    {
        if (_leavingGameplay||_sceneChanging||_calibrationTransitionPending) return;
        if (_screen is GameplayScreen gameplay)
        {
            if (Settings!.Visible) gameplay.SuspendForSettings();
            else if (gameplay.InputBlocked) gameplay.BeginResumeCountdown();
            return;
        }
        if (Settings!.Visible && Rhythm!.IsRunning && !Rhythm.IsManuallyPaused)
        { _settingsPausedPlayback = true; Rhythm.PauseSong(); }
        else if (!Settings.Visible && _settingsPausedPlayback)
        { _settingsPausedPlayback = false; Rhythm!.ResumeSong(); }
        if (_screen is LobbyScreen lobby) lobby.MenuInputEnabled = !Settings!.Visible;
        else if (_screen is TitleScreen title) title.SetMenuInputEnabled(!Settings!.Visible);
    }
    private void LeaveGameplay()
    {
        if (_screen is not GameplayScreen gameplay) return;
        if(gameplay is TutorialGameplayScreen tutorial&&!tutorial.IsReplay)return;
        // Hide the drawer as part of navigation, never through the normal resume path.
        _leavingGameplay = true;
        try { gameplay.StopGameplay(); if(gameplay is TutorialGameplayScreen)ReturnFromTutorial(true);else GoToLobby(); }
        finally { _leavingGameplay = false; }
    }
    private void StartRequested()
    {
        try
        {
            bool calibrated=SettingsService.CalibrationCompleted,tutorial=TutorialGameplayScreen.HasCompleted;
            GD.Print($"[TITLE] CalibrationCompleted = {calibrated}");
            GD.Print($"[TITLE] TutorialCompleted = {tutorial}");
            GD.Print($"[TITLE] TutorialEnabled = {BuildFeatures.TutorialEnabled}");
            GD.Print($"[TITLE] NextScene = {(!calibrated?"Calibration":!tutorial&&BuildFeatures.TutorialEnabled?"Tutorial":"Song Select")}");
            Callable.From(()=>NavigateTitle(()=>{if(calibrated)EnterAfterCalibration();else GoToCalibration(CalibrationReason.FirstLaunch);})).CallDeferred();
        }
        catch(Exception error)
        {
            GD.PushError("[TITLE] Invalid onboarding state; falling back to Calibration: "+error);
            Callable.From(()=>NavigateTitle(()=>GoToCalibration(CalibrationReason.FirstLaunch))).CallDeferred();
        }
    }
    private void NavigateTitle(Action navigate)
    {
        try{navigate();}
        catch(Exception error){GD.PushError("[TITLE] Navigation failed: "+error);}
    }
    private void EnterAfterCalibration(){if(!BuildFeatures.TutorialEnabled||TutorialGameplayScreen.HasCompleted)GoToLobby();else GoToTutorial(false);}
    private bool _tutorialAfterCalibration;
    private int _tutorialReturnSelection;
    private void ReplayTutorial()
    {
        if(!BuildFeatures.TutorialEnabled||_screen is TutorialGameplayScreen)return;
        _tutorialReturnSelection=_screen is LobbyScreen lobby?lobby.SelectedIndex:0;_tutorialReturnCustom=_screen is LobbyScreen category&&category.CustomCategory;
        if(!SettingsService.CalibrationCompleted){_tutorialAfterCalibration=true;GoToCalibration(CalibrationReason.Settings);}
        else GoToTutorial(true);
    }
    public void GoToTutorial(bool replay=false)
    {
        if(!BuildFeatures.TutorialEnabled){GoToLobby();return;}
        if(!SettingsService.CalibrationCompleted)
        {_tutorialAfterCalibration=replay;GoToCalibration(replay?CalibrationReason.Settings:CalibrationReason.FirstLaunch);return;}
        var scene=LoadScene("res://game/tutorial/TutorialGameplay.tscn");
        var tutorial=scene.Instantiate<TutorialGameplayScreen>();tutorial.IsReplay=replay;
        _leavingGameplay=true;
        try
        {
            Settings!.CloseImmediately();Rhythm!.StopSong();Rhythm.Chart=null;Music!.Stream=null;
            if(_screen is GameplayScreen oldGame)oldGame.StopGameplay();
            if(_screen!=null){ScreenHost!.RemoveChild(_screen);_screen.QueueFree();}
            _screen=tutorial;ScreenHost!.AddChild(tutorial);ReleaseDiagnostics.Scene("switched","res://game/tutorial/TutorialGameplay.tscn");Settings.TutorialButton.Disabled=true;
            Settings.SetSongTitle(tutorial.Data?.SongName);Settings.LeaveButton.Visible=replay;
            tutorial.SettingsRequested+=()=>{Settings.SetSongTitle(tutorial.Data?.SongName);Settings.LeaveButton.Visible=replay;Settings.Open();};
            tutorial.LobbyRequested+=()=>ReturnFromTutorial(replay);
            tutorial.TutorialCompleted+=()=>Callable.From(()=>ReturnFromTutorial(replay)).CallDeferred();
        }finally{_leavingGameplay=false;}
    }
    private void ReturnFromTutorial(bool replay)
    {
        Settings!.TutorialButton.Disabled=false;
        GoToLobby();if(replay){((LobbyScreen)_screen!).SelectCategory(_tutorialReturnCustom);((LobbyScreen)_screen!).Navigate(_tutorialReturnSelection);}
    }
    public void GoToTitle()
    {
        Settings!.SetSongTitle(null);
        var screen = Replace<TitleScreen>("res://game/title/Title.tscn");
        screen.StartRequested += StartRequested; screen.SettingsRequested += Settings!.Open;
    }
    public void GoToLobby()
    {
        bool custom=_screen is GameplayScreen previous&&Gamejam2.Data.CustomMapRegistry.IsCustom(previous.SongId);
        Settings!.SetSongTitle(null);
        var screen = Replace<LobbyScreen>("res://game/lobby/Lobby.tscn");
        screen.SelectCategory(custom);
        screen.TitleRequested += GoToTitle; screen.StageSelected += GoToGameplay;
    }
    private void CalibrateFromSettings()
    {
        GD.Print("[CALIBRATION] handler entered");
        if(_calibrationTransitionPending){GD.Print("[CALIBRATION] blocked by state: transition pending");return;}
        _calibrationTransitionPending=true;
        // Clear stale popup/return state; current input domains do not own navigation.
        _settingsPausedPlayback=false;_leavingGameplay=false;_tutorialAfterCalibration=false;
        _sceneChanging=true;
        GD.Print("[CALIBRATION] Settings close started");
        Settings!.CloseImmediately();
        GD.Print("[CALIBRATION] Settings close finished");
        Callable.From(()=>
        {
            try
            {
                GD.Print("[CALIBRATION] Calibration load requested");
                GoToCalibration(CalibrationReason.Settings);
                GD.Print("[CALIBRATION] scene switched");
            }
            catch(Exception error)
            {
                GD.PushError("[CALIBRATION] load failed: res://game/calibration/Calibration.tscn; "+error);
            }
            finally
            {
                _sceneChanging=false;_calibrationTransitionPending=false;
                Settings!.CompleteCalibrationRequest();
                UpdateMenuInput();
                GD.Print("[CALIBRATION] transition state cleared");
            }
        }).CallDeferred();
    }
    public void GoToCalibration(CalibrationReason reason)
    {
        if (_screen is GameplayScreen gameplay)
        {
            gameplay.SuspendForSettings();
            _suspendedGameplay = gameplay;
            ScreenHost!.RemoveChild(gameplay); _screen = null;
        }
        _reason = reason; _returnToLobby = _screen is LobbyScreen;
        _returnLobbySelection = _screen is LobbyScreen lobby ? lobby.SelectedIndex : 0;
        _returnLobbyCustom=_screen is LobbyScreen category&&category.CustomCategory;
        var screen = Replace<CalibrationScreen>("res://game/calibration/Calibration.tscn");
        GD.Print("[CALIBRATION] Calibration scene loaded");
        screen.Completed += () => Callable.From(CalibrationCompleted).CallDeferred();
        screen.Initialize(Rhythm!, Music!, force: true);
    }
    private void CalibrationCompleted()
    {
        SettingsService.CompleteCalibration(); Rhythm!.UserOffsetSeconds = SettingsService.InputOffsetSeconds;
        if (_suspendedGameplay != null)
        {
            Settings!.CloseImmediately(); Rhythm!.StopSong(); Rhythm.Chart=null; Music!.Stream=null;
            if (_screen != null) { ScreenHost!.RemoveChild(_screen); _screen.QueueFree(); }
            _screen=_suspendedGameplay; _suspendedGameplay=null; ScreenHost!.AddChild(_screen);
            Settings.SetSongTitle(((GameplayScreen)_screen).Data.SongName); if(_screen is TutorialGameplayScreen tutorial)Settings.LeaveButton.Visible=tutorial.IsReplay;Settings.Open();
        }
        else if(_tutorialAfterCalibration){_tutorialAfterCalibration=false;GoToTutorial(true);}
        else if (_reason == CalibrationReason.FirstLaunch) EnterAfterCalibration();
        else { if (_returnToLobby) { GoToLobby(); ((LobbyScreen)_screen!).SelectCategory(_returnLobbyCustom);((LobbyScreen)_screen!).Navigate(_returnLobbySelection); } else GoToTitle(); Settings!.Open(); }
    }
    /// <summary>Song selection loads the authored current scene; missing configuration is a development error.</summary>
    public void GoToGameplay(string stageId)
    {
        if (_screen is not LobbyScreen lobby || !(Gamejam2.Data.CustomMapRegistry.Find(stageId)!=null||ProgressService.IsUnlocked(stageId))) return;
        var stage=lobby.Catalog.Stages.FirstOrDefault(item=>item.StageId==stageId);
        if(stage==null||!stage.IsAvailable) return;
        if(string.IsNullOrWhiteSpace(stage.ScenePath)) {GD.PushError($"Available stage missing ScenePath: {stageId}");return;}
        var packed=LoadScene(stage.ScenePath);
        if(packed==null){GD.PushError($"Stage scene missing: {stageId}: {stage.ScenePath}");return;}
        var gameplay=packed.Instantiate<GameplayScreen>(); gameplay.SongId=stage.SongId;
        Settings!.CloseImmediately(); Rhythm!.StopSong();
        ScreenHost!.RemoveChild(_screen!); _screen!.QueueFree(); _screen=gameplay; ScreenHost.AddChild(gameplay);ReleaseDiagnostics.Scene("switched",stage.ScenePath);
        Settings.SetSongTitle(gameplay.Data?.SongName);
        gameplay.SettingsRequested+=Settings.Open; gameplay.LobbyRequested+=GoToLobby;
    }
    private static PackedScene LoadScene(string path)
    {
        ReleaseDiagnostics.Scene("requested",path);
        var scene=ResourceLoader.Load<PackedScene>(path)??throw new InvalidOperationException("Navigation scene load failed: "+path);
        ReleaseDiagnostics.Scene("loaded",path);return scene;
    }
    private T Replace<T>(string path) where T : Control
    {
        // Validate/instantiate before removing the current screen, so failed loads stay recoverable.
        var next = LoadScene(path).Instantiate<T>();
        Settings!.CloseImmediately(); Rhythm!.StopSong(); Rhythm.Chart = null; Music!.Stream = null;
        if (_screen is GameplayScreen gameplay) gameplay.StopGameplay();
        if (_screen != null) { ScreenHost!.RemoveChild(_screen); _screen.QueueFree(); }
        _screen = next; ScreenHost!.AddChild(next);ReleaseDiagnostics.Scene("switched",path); return next;
    }
    public override void _ExitTree()
    {
        _debugToast?.Kill(); if (Settings != null) { Settings.RecalibrationRequested -= CalibrateFromSettings; Settings.VisibilityChanged -= UpdateMenuInput; Settings.LeaveGameplayRequested -= LeaveGameplay;Settings.TutorialRequested-=ReplayTutorial; } Rhythm?.StopSong(); }
}
