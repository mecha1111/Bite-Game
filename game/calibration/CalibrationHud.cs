using System;
using System.Linq;
using Godot;
using Gamejam2.Settings;
namespace Gamejam2.Calibration;
/// <summary>플레이어 보정 화면. 측정/offset 계산은 RhythmCalibration에만 요청한다.</summary>
public partial class CalibrationHud : CanvasLayer
{
    private RhythmCalibration? _calibration;
    [Export] public Label TitleLabel { get; set; } = null!;
    [Export] public Label InformationLabel { get; set; } = null!;
    [Export] public Label ProgressLabel { get; set; } = null!;
    [Export] public Label CountLabel { get; set; } = null!;
    [Export] public Label InputLabel { get; set; } = null!;
    [Export] public Label StatusLabel { get; set; } = null!;
    [Export] public Label ResultLabel { get; set; } = null!;
    [Export] public TimingMeter Meter { get; set; } = null!;
    private CalibrationStage? _previousStage;
    private double? _lastInputTime;
    [Export] public CalibrationIndicator Indicator { get; set; } = null!;
    [Export] public Button StartButton { get; set; } = null!;
    [Export] public Button RepeatButton { get; set; } = null!;
    [Export] public Button ApplyButton { get; set; } = null!;
    [Export] public Button SkipButton { get; set; } = null!;
    [Export] public Button ConfirmButton { get; set; } = null!;
    [Export] public Button FinishButton { get; set; } = null!;
    public string InformationText => InformationLabel.Text;
    public void Initialize(RhythmCalibration calibration)
    {
        _calibration = calibration;
        Meter.Configure(calibration.ReferenceWindows);
        calibration.SampleAccepted += OnSampleAccepted;
        StartButton.Pressed += () => Act(calibration.BeginAudioCalibration);
        RepeatButton.Pressed += () => Act(calibration.BeginAudioCalibration);
        ApplyButton.Pressed += () => Act(calibration.ApplySuggestionAndBeginVisual);
        SkipButton.Pressed += () => Act(() =>
        {
            if (calibration.Stage == CalibrationStage.Visual) calibration.SkipVisualWithDefaults();
            else calibration.SkipAudioWithDefaults();
        });
        ConfirmButton.Pressed += () => Act(calibration.BeginConfirmation);
        FinishButton.Pressed += () => Act(calibration.Finish);
        BuildVisualControls();
        calibration.StageChanged += UpdateStage; UpdateStage();
    }
    private HBoxContainer _visualControls=null!;
    private Label _visualValue=null!;
    private void BuildVisualControls()
    {
        var layout=InformationLabel.GetParent<Control>();
        _visualControls=new HBoxContainer{Name="VisualOffsetControls",Position=new Vector2(350,610),Size=new Vector2(580,64),Alignment=BoxContainer.AlignmentMode.Center};
        layout.AddChild(_visualControls);
        _visualValue=new Label{CustomMinimumSize=new Vector2(260,60),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        foreach(int direction in new[]{-1,1})
        {
            if(direction==1)_visualControls.AddChild(_visualValue);
            var button=new Button{Text=direction<0?"−":"+",CustomMinimumSize=new Vector2(80,60)};
            var style=new StyleBoxFlat{BgColor=new Color(.025f,.12f,.17f),BorderColor=new Color(.15f,.5f,.6f)};style.SetBorderWidthAll(2);button.AddThemeStyleboxOverride("normal",style);button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color",new Color(.6f,1,1));button.AddThemeFontOverride("font",InformationLabel.GetThemeFont("font"));button.AddThemeFontSizeOverride("font_size",32);
            button.Pressed+=()=>{_calibration!.SetVisualOffset(CalibrationSession.VisualOffsetSeconds+direction*.005);UpdateVisualValue();};_visualControls.AddChild(button);
        }
        _visualValue.AddThemeFontOverride("font",InformationLabel.GetThemeFont("font"));_visualValue.AddThemeFontSizeOverride("font_size",26);UpdateVisualValue();
    }
    private void UpdateVisualValue()=>_visualValue.Text=$"화면 {CalibrationSession.VisualOffsetSeconds*1000:+0;-0;0} ms";
    private void Act(Action action) { GetViewport().GuiReleaseFocus(); action(); }
    private void UpdateStage()
    {
        if (_calibration == null) return;
        var stage = _calibration.Stage;
        _visualControls.Visible=stage==CalibrationStage.Visual;
        SetCaption(ApplyButton,"화면 보정으로");SetCaption(ConfirmButton,"확인");SetCaption(FinishButton,"적용");SetCaption(RepeatButton,"다시 맞추기");
        Visible = stage != CalibrationStage.Complete;
        StartButton.GetParent<Control>().Visible = stage == CalibrationStage.Entry;
        RepeatButton.GetParent<Control>().Visible = stage is CalibrationStage.CountIn or CalibrationStage.AudioSampling or CalibrationStage.AudioResult or CalibrationStage.ConfirmationResult;
        ApplyButton.GetParent<Control>().Visible = stage == CalibrationStage.AudioResult && _calibration.HasReliableEstimate;
        SkipButton.GetParent<Control>().Visible = stage is CalibrationStage.Entry or CalibrationStage.CountIn or CalibrationStage.AudioSampling or CalibrationStage.Visual;
        ConfirmButton.GetParent<Control>().Visible = stage == CalibrationStage.Visual;
        FinishButton.GetParent<Control>().Visible = stage == CalibrationStage.ConfirmationResult;
        if (_previousStage != stage)
        {
            if (stage is CalibrationStage.Entry or CalibrationStage.CountIn or CalibrationStage.Confirmation)
            { Meter.Reset(); _lastInputTime = null; }
            if (stage is CalibrationStage.AudioResult or CalibrationStage.ConfirmationResult)
                Meter.ShowDistribution(_calibration.Samples, _calibration.Summary.MedianSeconds);
            _previousStage = stage;
        }
        bool preparing = stage == CalibrationStage.CountIn;
        CountLabel.Visible = preparing;
        Meter.Modulate = new Color(1, 1, 1, preparing || stage == CalibrationStage.Entry ? .4f : 1);
        ProgressLabel.Modulate = new Color(1, 1, 1, preparing ? .35f : 1);
        RepeatButton.GetParent<Control>().Modulate = new Color(1, 1, 1, stage == CalibrationStage.AudioResult ? .6f : 1);
        SkipButton.GetParent<Control>().Modulate = new Color(1, 1, 1, .5f);
        // Result actions read primary first, followed by the quiet remeasurement action.
        var actions = ApplyButton.GetParent().GetParent();
        actions.MoveChild(ApplyButton.GetParent(), stage == CalibrationStage.AudioResult ? 0 : 2);
        if (stage is CalibrationStage.Entry or CalibrationStage.CountIn) Meter.ErrorLabel.Text = "";
        SetCaption(SkipButton, stage == CalibrationStage.Visual ? "기본값 사용하기"
            : stage is CalibrationStage.CountIn or CalibrationStage.AudioSampling ? "기본값 사용" : "기본값으로 시작");
        UpdateInformation();
    }
    private static void SetCaption(Button button, string text)
    {
        var group = button.GetParent();
        group.GetNode<Label>("Caption").Text = text;
        for (int i = 0; i < 8; i++) group.GetNode<Label>($"TextBacking{i}").Text = text;
    }
    private void UpdateInformation()
    {
        if (_calibration == null) return;
        TitleLabel.Text = "싱크 조정";
        InformationLabel.Text = _calibration.Stage switch
        {
            CalibrationStage.CountIn => "박자를 들어보세요",
            CalibrationStage.AudioResult => _calibration.HasReliableEstimate
                ? "싱크 조정 완료"
                : "입력이 일정하지 않았습니다. 다시 측정해주세요.",
            CalibrationStage.Visual => "화면과 소리가 동시에 느껴지도록 맞춰주세요",
            CalibrationStage.ConfirmationResult => "보정 확인이 완료되었습니다.",
            _ => "소리에 맞춰 버튼을 눌러주세요"
        };
        ProgressLabel.Text = _calibration.Stage switch
        {
            CalibrationStage.AudioSampling => $"{_calibration.Samples.Count} / {RhythmCalibration.AudioTapCount}",
            CalibrationStage.Confirmation => $"{_calibration.Samples.Count} / {RhythmCalibration.ConfirmationTapCount}",
            CalibrationStage.Entry or CalibrationStage.CountIn => "0 / 12",
            _ => ""
        };
        InputLabel.Text = $"{SettingsService.RhythmKey.ToString().ToUpperInvariant()} · 마우스 · 컨트롤러";
        bool result = _calibration.Stage is CalibrationStage.AudioResult or CalibrationStage.ConfirmationResult;
        ResultLabel.Visible = result;
        ResultLabel.Text = result && _calibration.HasReliableEstimate
            ? $"입력 보정  {_calibration.SuggestedInputOffsetSeconds * 1000:+0;-0;+0} ms" : "";
        if(_calibration.Stage==CalibrationStage.ConfirmationResult)
            ResultLabel.AddThemeFontSizeOverride("font_size",28);
        if(_calibration.Stage==CalibrationStage.ConfirmationResult)ResultLabel.Text=$"Early {_calibration.Samples.Count(x=>x<0)} · Late {_calibration.Samples.Count(x=>x>0)} · 중앙 오차 {_calibration.Summary.MedianSeconds*1000:+0;-0;0} ms\nInput {CalibrationSession.UserInputOffsetSeconds*1000:+0;-0;0} ms · Visual {CalibrationSession.VisualOffsetSeconds*1000:+0;-0;0} ms";
        ResultLabel.AddThemeFontSizeOverride("font_size",_calibration.Stage==CalibrationStage.ConfirmationResult?28:40);
        ProgressLabel.Visible = !result&&_calibration.Stage!=CalibrationStage.Visual;
        StatusLabel.Text = _calibration.Stage switch
        {
            CalibrationStage.AudioSampling => "",
            CalibrationStage.AudioResult => "",
            CalibrationStage.Entry => "",
            _ => ""
        };
    }
    private void OnSampleAccepted(double errorSeconds)
    {
        if (_calibration == null) return;
        _lastInputTime = _calibration.SampleTimelineSeconds;
        Meter.Accept(errorSeconds, _lastInputTime.Value);
    }
    public override void _Process(double delta)
    {
        if (_calibration == null || !Visible) return;
        double clock = _calibration.SampleTimelineSeconds;
        double age = _lastInputTime.HasValue ? clock - _lastInputTime.Value : double.PositiveInfinity;
        bool preparing = _calibration.Stage == CalibrationStage.CountIn;
        CountLabel.Text = preparing ? _calibration.CountInBeatNumber.ToString() : "";
        if (preparing) InformationLabel.Text = "박자를 들어보세요";
        else if (_calibration.Stage == CalibrationStage.AudioSampling)
            InformationLabel.Text = clock < RhythmCalibration.FirstTapSeconds + .75 ? "이제 눌러주세요" : "소리에 맞춰 버튼을 눌러주세요";
        StatusLabel.Text = preparing ? "아직 누르지 마세요"
            : _calibration.Stage == CalibrationStage.AudioSampling && clock < RhythmCalibration.FirstTapSeconds + .75 ? "측정 시작" : "";
        Indicator.ShowPhase(_calibration.PresentationBeatPhase, age, !preparing);
        Meter.Refresh(clock);
    }
    public override void _ExitTree()
    {
        if (_calibration == null) return;
        _calibration.StageChanged -= UpdateStage;
        _calibration.SampleAccepted -= OnSampleAccepted;
    }
}
