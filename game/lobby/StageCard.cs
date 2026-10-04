using System;
using System.Linq;
using Godot;
namespace Gamejam2.Lobby;
/// <summary>scene-authored 환경 카드. 에디터도 같은 디자인 데이터를 표시하며 진행은 런타임에서만 전달받는다.</summary>
[Tool]
public partial class StageCard : Control
{
    [Export] public bool PreviewSelected { get; set; } = true;
    [Export] public StageData? Data { get; set; }
    [Export] public TextureRect Background { get; set; } = null!;
    [Export] public Control CompositeCover { get; set; } = null!;
    [Export] public Label ExBadge { get; set; } = null!;
    [Export] public Label NameLabel { get; set; } = null!;
    [Export] public Label StateLabel { get; set; } = null!;
    [Export] public Label DescriptionLabel { get; set; } = null!;
    [Export] public Button SelectButton { get; set; } = null!;
    [Export] public Control LockOverlay { get; set; } = null!;
    [Export] public CanvasItem SelectionFrame { get; set; } = null!;
    [Export] public ColorRect DarkOverlay { get; set; } = null!;
    [Export] public SwimmingMedalShark MedalShark { get; set; }=null!;
    public string StageId => Data?.StageId ?? "";
    public event Action<string>? Selected;
    private Label? _localTitle;
    private bool _unlocked, _selected = true, _interactionEnabled = true;
    [Export] public double EmphasisSeconds { get; set; } = .34;
    private Tween? _tween;
    public override void _Ready()
    {
        _selected = PreviewSelected;
        if (Data != null) Bind(Data, Data.UnlockRequirement.Length == 0);
        if (!Engine.IsEditorHint()) SelectButton.Pressed += () => Selected?.Invoke(StageId);
        SetProcess(Engine.IsEditorHint());
    }
    public override void _Process(double delta)
    {
        // Resource inspector edits must be visible before running, without creating UI nodes.
        if (Data != null && (NameLabel.Text != Data.DisplayName || Background.Texture != Data.BackgroundTexture || DescriptionLabel.Text != Data.OptionalDescription || CompositeCover.Visible != Data.IsEx || ExBadge.Text != Data.SecondaryTag))
            Bind(Data, Data.UnlockRequirement.Length == 0);
    }
    public void Bind(StageData data, bool unlocked)
    {
        Data = data;
        if(_localTitle==null){_localTitle=new Label{Name="LocalTitleCover",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore};Background.AddChild(_localTitle);_localTitle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_localTitle.AddThemeFontOverride("font",NameLabel.GetThemeFont("font"));_localTitle.AddThemeFontSizeOverride("font_size",42);_localTitle.AddThemeColorOverride("font_color",new Color(.65f,1,1));}
        _localTitle.Text=data.DisplayName;_localTitle.Visible=Gamejam2.Data.CustomMapRegistry.Find(data.SongId) is {Generated:true,CoverPath:""};
 _unlocked = unlocked;
        MedalShark.SetRank(Engine.IsEditorHint()?"none":(Gamejam2.Data.CustomMapRegistry.IsCustom(data.StageId)?Gamejam2.Data.CustomMapRegistry.BestRank(data.StageId):Gamejam2.Save.ProgressService.BestStageRank(data.StageId)),unlocked);
        Background.Texture = data.BackgroundTexture; Background.Visible=!data.IsEx;CompositeCover.Visible=data.IsEx;
        ExBadge.Text=data.SecondaryTag;ExBadge.Visible=data.SecondaryTag.Length>0;NameLabel.Text = data.DisplayName;
        for (int i = 0; i < 8; i++) GetNode<Label>($"TitleBand/TextBacking{i}").Text = data.DisplayName;
        DescriptionLabel.Text = data.OptionalDescription;
        StateLabel.Text = !unlocked ? "잠김" : data.IsAvailable ? "선택 가능" : "준비 중";
        RefreshInteraction();
        LockOverlay.Visible = !unlocked; UpdateEmphasis(false);
    }
    public void SetSelected(bool selected, bool animate)
    { _selected = selected; RefreshInteraction(); UpdateEmphasis(animate); }
    public void SetInteractionEnabled(bool enabled) { _interactionEnabled = enabled; RefreshInteraction(); }
    private void RefreshInteraction() => SelectButton.Disabled = !_interactionEnabled || (_selected && (!_unlocked || Data?.IsAvailable != true));
    private void UpdateEmphasis(bool animate)
    {
        SelectionFrame.Visible = _selected;
        _tween?.Kill();
        Color overlay = new(0, .025f, .06f, !_unlocked ? .52f : _selected ? .02f : .48f);
        Color title = new(1, 1, 1, _selected ? 1 : .6f);
        var materials = new[]{(ShaderMaterial)Background.Material}.Concat(CompositeCover.FindChildren("*","TextureRect",true,false).OfType<TextureRect>().Select(t=>(ShaderMaterial)t.Material)).Distinct().ToArray();
        float saturation = _selected ? 1 : .55f;
        if (!animate) { foreach(var material in materials)material.SetShaderParameter("saturation", saturation); DarkOverlay.Color = overlay; GetNode<Control>("TitleBand").Modulate = title; return; }
        _tween = CreateTween().SetParallel();
        foreach(var material in materials)_tween.TweenProperty(material, "shader_parameter/saturation", saturation, EmphasisSeconds);
        _tween.TweenProperty(DarkOverlay, "color", overlay, EmphasisSeconds);
        _tween.TweenProperty(GetNode<Control>("TitleBand"), "modulate", title, EmphasisSeconds);
    }
    public override void _ExitTree() => _tween?.Kill();
}
