using System;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Settings;
/// <summary>Scene-authored drawer. Owns presentation and settings binding, not screen routing.</summary>
public partial class SettingsPopup : Control
{
	[Export] public HSlider MasterSlider { get; set; } = null!;
	[Export] public HSlider MusicSlider { get; set; } = null!;
	[Export] public HSlider EffectsSlider { get; set; } = null!;
	[Export] public Label MasterAmount { get; set; } = null!;
	[Export] public Label MusicAmount { get; set; } = null!;
	[Export] public Label EffectsAmount { get; set; } = null!;
	[Export] public OffsetControl InputOffset { get; set; } = null!;
	[Export] public OffsetControl VisualOffset { get; set; } = null!;
	[Export] public Button TutorialButton { get; set; } = null!;
	public event Action? TutorialRequested;
	[Export] public Button RecalibrateButton { get; set; } = null!;
	[Export] public Button CloseButton { get; set; } = null!;
	[Export] public OptionButton DisplayMode { get; set; } = null!;
	[Export] public OptionButton ResolutionChoice { get; set; } = null!;
	[Export] public Control Drawer { get; set; } = null!;
	[Export] public ColorRect DimOverlay { get; set; } = null!;
	[Export] public ColorRect EdgeStreak { get; set; } = null!;
	[Export] public ScrollContainer Scroll { get; set; } = null!;
	[Export] public Control SongHeader { get; set; } = null!;
	[Export] public Label SongTitle { get; set; } = null!;
	[Export] public Button LeaveButton { get; set; } = null!;
	[Export] public Control ExitConfirmation { get; set; } = null!;
	[Export] public Button CancelExitButton { get; set; } = null!;
	[Export] public Button ConfirmExitButton { get; set; } = null!;
	private readonly Dictionary<Control, FocusModeEnum> _drawerFocus = new();
	public Button ResetProgressButton {get;private set;}=null!;
	public Control ResetConfirmation {get;private set;}=null!;
	public Button CancelResetButton {get;private set;}=null!;
	public Button ConfirmResetButton {get;private set;}=null!;
	private Label _resetFeedback=null!;
	private Tween? _feedbackTween;
	public event Action? LeaveGameplayRequested;
	[Export] public double OpenSeconds { get; set; } = 0.34;
	[Export] public double CloseSeconds { get; set; } = 0.22;
	[Export(PropertyHint.Range, "0,1")] public float DimAmount { get; set; } = 0.56f;
	[Export] public float OvershootPixels { get; set; } = 8;
	private bool _refreshing, _closing, _calibrationRequestPending;
	private bool _syncSubscribed,_syncInitialized;
	private float _homeX;
	private Tween? _slide;
	public event Action? RecalibrationRequested;
	public override void _Ready()
	{
		_homeX = Drawer.Position.X;
		TutorialButton.GetParent<Control>().Visible=Gamejam2.Startup.BuildFeatures.TutorialEnabled;
		Visible = false;
		DimOverlay.Color = new Color(0, 0.01f, 0.025f, 1);
		BindVolume(MasterSlider, MasterAmount, SettingsService.SetMasterVolume);
		BindVolume(MusicSlider, MusicAmount, SettingsService.SetMusicVolume);
		BindVolume(EffectsSlider, EffectsAmount, SettingsService.SetEffectsVolume);
		InputOffset.ValueChanged += value => { if (!_refreshing) SettingsService.InputOffsetSeconds = value / 1000; };
		VisualOffset.ValueChanged += value => { if (!_refreshing) SettingsService.VisualOffsetSeconds = value / 1000; };
		TutorialButton.Pressed += () => { if (!_closing&&Gamejam2.Startup.BuildFeatures.TutorialEnabled) TutorialRequested?.Invoke(); };
		_syncInitialized=true;SubscribeCalibration();
		CloseButton.Pressed += Close;
		LeaveButton.Pressed += AskToLeave;
		CancelExitButton.Pressed += CancelLeave;
		ConfirmExitButton.Pressed += () => { if (ExitConfirmation.Visible) LeaveGameplayRequested?.Invoke(); };
		ResetProgressButton=GetNode<Button>("Drawer/Margins/Layout/ScrollContainer/SettingsContent/DataSection/Reset/ResetButton");
		ResetConfirmation=GetNode<Control>("ResetConfirmation");CancelResetButton=GetNode<Button>("ResetConfirmation/Panel/Margins/Content/Actions/Cancel");ConfirmResetButton=GetNode<Button>("ResetConfirmation/Panel/Margins/Content/Actions/Confirm");
		_resetFeedback=GetNode<Label>("Drawer/Margins/Layout/ScrollContainer/SettingsContent/DataSection/Feedback");
		ResetProgressButton.Pressed+=AskReset;CancelResetButton.Pressed+=CancelReset;ConfirmResetButton.Pressed+=ConfirmReset;
		DisplayMode.ItemSelected += index => { if (!_refreshing) SettingsService.SetFullscreen(index == 1); ResolutionChoice.Disabled = SettingsService.Fullscreen; };
		ResolutionChoice.ItemSelected += index => { if (!_refreshing) SettingsService.SetResolution((int)index); };
		DimOverlay.GuiInput += input =>
		{
			if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
				or InputEventScreenTouch { Pressed: true })
			{ AcceptEvent(); Close(); }
		};
	}
	private void SubscribeCalibration()
	{
		if(_syncSubscribed||!GodotObject.IsInstanceValid(RecalibrateButton))return;
		if(!RecalibrateButton.IsConnected(Button.SignalName.Pressed,Callable.From(RequestCalibration)))
			RecalibrateButton.Pressed+=RequestCalibration;
		_syncSubscribed=true;
	}
	public override void _EnterTree(){if(_syncInitialized)SubscribeCalibration();}

	private void RequestCalibration()
	{
		GD.Print("[CALIBRATION] RecalibrateButton pressed");
		if(_calibrationRequestPending){GD.Print("[CALIBRATION] blocked by state: request pending");return;}
		if(RecalibrationRequested==null){GD.PushError("Calibration request has no SceneRouter subscriber: "+GetPath());return;}
		_calibrationRequestPending=true;RecalibrateButton.Disabled=true;
		_slide?.Kill();_slide=null;_closing=false;
		try{RecalibrationRequested.Invoke();}
		catch{CompleteCalibrationRequest();throw;}
	}
	public void CompleteCalibrationRequest()
	{_calibrationRequestPending=false;RecalibrateButton.Disabled=false;}

	private void BindVolume(HSlider slider, Label amount, Action<double> changed)
	{ slider.ValueChanged += value => { amount.Text = $"{value:0}%"; if (!_refreshing) changed(value / 100); }; }
	/// <summary>Caller supplies song metadata; an absent song leaves no empty header.</summary>
	public void SetSongTitle(string? displayName)
	{ SongTitle.Text = displayName ?? ""; SongHeader.Visible = !string.IsNullOrWhiteSpace(displayName); LeaveButton.Visible = displayName != null; }
	private void AskToLeave()
	{
		if (_closing || !Visible || !LeaveButton.Visible || ExitConfirmation.Visible || ResetConfirmation.Visible) return;
		ExitConfirmation.Visible = true;
		BlockDrawerFocus(); CancelExitButton.GrabFocus();
	}
	private void BlockDrawerFocus()
	{
		void BlockFocus(Node node)
		{
			if (node is Control control) { _drawerFocus[control] = control.FocusMode; control.FocusMode = FocusModeEnum.None; }
			foreach (var child in node.GetChildren()) BlockFocus(child);
		}
		BlockFocus(Drawer);
	}
	private void RestoreDrawerFocus(){foreach(var pair in _drawerFocus)pair.Key.FocusMode=pair.Value;_drawerFocus.Clear();}
	private void AskReset()
	{if(_closing||!Visible||ExitConfirmation.Visible||ResetConfirmation.Visible)return;ResetConfirmation.Show();BlockDrawerFocus();CancelResetButton.GrabFocus();}
	private void CancelReset(){ResetConfirmation.Hide();RestoreDrawerFocus();if(Visible)ResetProgressButton.GrabFocus();}
	private void ConfirmReset()
	{
		if(!ResetConfirmation.Visible)return;
		Gamejam2.Save.ProgressService.ResetProgress();CancelReset();
		_resetFeedback.Text="진행도가 초기화되었습니다."+(Gamejam2.Save.ProgressService.DebugUnlockAllStages?"\n[DEBUG] 해금 표시 유지":"");_resetFeedback.Show();
		_feedbackTween?.Kill();_feedbackTween=CreateTween();_feedbackTween.TweenInterval(1.7);_feedbackTween.TweenCallback(Callable.From(()=>_resetFeedback.Hide()));
	}
	private void CancelLeave()
	{
		ExitConfirmation.Visible = false;
		RestoreDrawerFocus();
		if (Visible && LeaveButton.Visible) LeaveButton.GrabFocus();
	}
	public void Open()
	{
		if(_calibrationRequestPending)return;
		_refreshing = true;
		DisplayMode.Select(SettingsService.Fullscreen ? 1 : 0);
		ResolutionChoice.Select(Array.IndexOf(SettingsService.SupportedResolutions, SettingsService.Resolution));
		ResolutionChoice.Disabled = SettingsService.Fullscreen;
		MasterSlider.Value = SettingsService.MasterVolume * 100;
		MusicSlider.Value = SettingsService.MusicVolume * 100;
		EffectsSlider.Value = SettingsService.EffectsVolume * 100;
		MasterAmount.Text = $"{MasterSlider.Value:0}%";
		MusicAmount.Text = $"{MusicSlider.Value:0}%";
		EffectsAmount.Text = $"{EffectsSlider.Value:0}%";
		InputOffset.Value = SettingsService.InputOffsetSeconds * 1000;
		VisualOffset.Value = SettingsService.VisualOffsetSeconds * 1000;
		_refreshing = false;
		_slide?.Kill(); _closing = false;
		if (!Visible)
		{
			Drawer.Position = new Vector2(_homeX - Drawer.Size.X - 16, Drawer.Position.Y);
			DimOverlay.Modulate = new Color(1, 1, 1, 0);
		}
		Visible = true;
		Scroll.ScrollVertical = 0;
		double settle = OpenSeconds * 0.24;
		_slide = CreateTween().SetParallel();
		_slide.TweenProperty(Drawer, "position:x", _homeX + OvershootPixels, OpenSeconds - settle).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_slide.TweenProperty(DimOverlay, "modulate:a", DimAmount, OpenSeconds - settle);
		_slide.TweenProperty(EdgeStreak, "modulate:a", 0.65, OpenSeconds - settle);
		_slide.Chain().TweenProperty(Drawer, "position:x", _homeX, settle).SetTrans(Tween.TransitionType.Sine);
		_slide.Parallel().TweenProperty(EdgeStreak, "modulate:a", 0, settle);
		Callable.From(() => { if (IsInsideTree() && Visible && !_closing) DisplayMode.GrabFocus(); }).CallDeferred();
	}
	public void Close()
	{
		if(_calibrationRequestPending)return;
		if(ResetConfirmation.Visible){CancelReset();return;}
		if (ExitConfirmation.Visible) { CancelLeave(); return; }
		if (!Visible || _closing) return;
		_closing = true; _slide?.Kill();
		_slide = CreateTween().SetParallel();
		_slide.TweenProperty(Drawer, "position:x", _homeX - Drawer.Size.X - 16, CloseSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_slide.TweenProperty(DimOverlay, "modulate:a", 0, CloseSeconds);
		_slide.TweenProperty(EdgeStreak, "modulate:a", 0, CloseSeconds);
		_slide.Chain().TweenCallback(Callable.From(CloseImmediately));
	}
	/// <summary>Scene transitions remove the overlay immediately, rather than leaving it over calibration.</summary>
	public void CloseImmediately()
	{
		if(ResetConfirmation.Visible)CancelReset();
		if (ExitConfirmation.Visible) CancelLeave();
		_slide?.Kill(); _slide = null; _closing = false;
		if (Visible) GetViewport().GuiReleaseFocus();
		Visible = false;
		Drawer.Position = new Vector2(_homeX, Drawer.Position.Y);
		DimOverlay.Modulate = new Color(1, 1, 1, 0);
		EdgeStreak.Modulate = new Color(1, 1, 1, 0);
	}
	public override void _Input(InputEvent input)
	{
		if (!Visible || !(ExitConfirmation.Visible||ResetConfirmation.Visible) || input.IsEcho()) return;
		if (input.IsActionPressed("ui_cancel")) { if(ResetConfirmation.Visible)CancelReset();else CancelLeave(); GetViewport().SetInputAsHandled(); }
		else if (input.IsActionPressed("ui_focus_next") || input.IsActionPressed("ui_focus_prev"))
		{
			var cancel=ResetConfirmation.Visible?CancelResetButton:CancelExitButton;var confirm=ResetConfirmation.Visible?ConfirmResetButton:ConfirmExitButton;
			if (cancel.HasFocus()) confirm.GrabFocus(); else cancel.GrabFocus();
			GetViewport().SetInputAsHandled();
		}
	}
	public override void _UnhandledKeyInput(InputEvent input)
	{
		// A dropdown consumes Escape first; otherwise it closes this drawer.
		if (!Visible || input.IsEcho() || !input.IsActionPressed("ui_cancel")) return;
		Close(); GetViewport().SetInputAsHandled();
	}
	public override void _ExitTree()
	{
		if(_syncSubscribed&&GodotObject.IsInstanceValid(RecalibrateButton)&&RecalibrateButton.IsConnected(Button.SignalName.Pressed,Callable.From(RequestCalibration)))
			RecalibrateButton.Pressed-=RequestCalibration;
		_syncSubscribed=false;_slide?.Kill();_feedbackTween?.Kill();
	}
}
