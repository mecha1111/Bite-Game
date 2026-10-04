using System;
using System.Linq;
using Godot;
using Gamejam2.Save;
namespace Gamejam2.Lobby;
/// <summary>단일 선택 index의 수평 곡 선택. 반복 UI는 PackedScene, timing/gameplay는 소유하지 않는다.</summary>
public partial class LobbyScreen : Control
{
    [Export] public StageCatalog Catalog { get; set; } = null!;
    [Export] public PackedScene CardScene { get; set; } = null!;
    [Export] public Control FocusAnchor { get; set; } = null!;
    [Export] public Control Cards { get; set; } = null!;
    [Export] public Label Information { get; set; } = null!;
    [Export] public Label PositionLabel { get; set; } = null!;
    [Export] public Button TitleButton { get; set; } = null!;
    [Export] public NavigationArrow LeftArrow { get; set; } = null!;
    [Export] public NavigationArrow RightArrow { get; set; } = null!;
    [Export] public Control EnvironmentLayers { get; set; } = null!;
    [Export] public ShaderMaterial TransitionMaterial { get; set; } = null!;
    [Export] public CpuParticles2D TransitionBubbles { get; set; } = null!;
    [Export] public Node2D Ambience { get; set; } = null!;
    [Export] public float BackdropOpacity { get; set; } = .25f;
    [Export] public float CardSpacing { get; set; } = 1090;
    [Export] public float SideScale { get; set; } = .86f;
    [Export] public double TransitionSeconds { get; set; } = .34;
    public int SelectedIndex { get; private set; }
    private bool _menuInputEnabled = true;
    public bool MenuInputEnabled
    {
        get => _menuInputEnabled;
        set
        {
            if (_menuInputEnabled == value) return;
            _menuInputEnabled = value;
            UpdateInteraction();
            if (value) Callable.From(RestoreCardFocus).CallDeferred();
        }
    }
    public event Action<string>? StageSelected;
    public event Action? TitleRequested;
    public event Action? KeyboardAction;
    private StageCatalog _mainCatalog=null!;
    public bool CustomCategory {get;private set;}
    private Tween? _categoryTween;
    private Vector2 _cardsOrigin;
    public void SelectCategory(bool custom)=>Category(custom&&Gamejam2.Data.CustomMapRegistry.Maps.Count>0);
    private bool Available(string id)=>Gamejam2.Data.CustomMapRegistry.IsUnlocked(id)||(!Gamejam2.Data.CustomMapRegistry.IsCustom(id)&&ProgressService.IsUnlocked(id));
    private void Category(bool custom)
    {
        if(!MenuInputEnabled||custom==CustomCategory)return;CustomCategory=custom;Catalog=custom?Gamejam2.Data.CustomMapRegistry.Catalog():_mainCatalog;SelectedIndex=0;Refresh();
        GetNode<Button>("Categories/Main").SelfModulate=custom?new Color(.65f,.75f,.8f):new Color(.6f,1,1);
        GetNode<Button>("Categories/Custom").SelfModulate=custom?new Color(.6f,1,1):new Color(.65f,.75f,.8f);
        foreach(string name in new[]{"Main","Custom"})
        {var b=GetNode<Button>("Categories/"+name);b.PivotOffset=b.Size*.5f;b.Scale=Vector2.One*((name=="Custom")==custom?1.04f:1);b.GetNode<Control>("SelectedAccent").Visible=(name=="Custom")==custom;}
        _categoryTween?.Kill();Cards.Position=_cardsOrigin+new Vector2(custom?48:-48,0);Cards.Modulate=new Color(1,1,1,0);
        _categoryTween=CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _categoryTween.TweenProperty(Cards,"position",_cardsOrigin,.28);_categoryTween.TweenProperty(Cards,"modulate",Colors.White,.28);
        TransitionEnvironment(true,custom?1:-1);
    }
    private CanvasLayer? _generator;
    private Button? _addMusic,_deleteMusic;
    private void InstallGenerator()
    {
        const string path="res://custom_map_generator/GeneratorPanel.tscn";
        if(!ResourceLoader.Exists(path))return;
        _generator=GD.Load<PackedScene>(path).Instantiate<CanvasLayer>();AddChild(_generator);
        _generator.Connect("MapsChanged",Callable.From(()=>{Catalog=Gamejam2.Data.CustomMapRegistry.Catalog();SelectedIndex=0;Refresh();}));
        var row=new HBoxContainer{Name="LocalMusicActions",Position=new Vector2(1330,160),Size=new Vector2(460,54)};AddChild(row);
        _addMusic=new Button{Text="내 음악 추가",CustomMinimumSize=new Vector2(250,54)};_deleteMusic=new Button{Text="삭제",CustomMinimumSize=new Vector2(140,54)};
        foreach(var b in new[]{_addMusic,_deleteMusic}){var style=new StyleBoxFlat{BgColor=new Color(.025f,.12f,.17f),BorderColor=new Color(.15f,.5f,.6f)};style.SetBorderWidthAll(2);b.AddThemeStyleboxOverride("normal",style);b.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());b.AddThemeFontOverride("font",GD.Load<Font>("res://assets/fonts/neodgm.ttf"));b.AddThemeFontSizeOverride("font_size",26);b.AddThemeColorOverride("font_color",new Color(.6f,1,1));row.AddChild(b);}
        _addMusic.Pressed+=()=>{if(MenuInputEnabled)_generator.Call("Open");};
        _deleteMusic.Pressed+=()=>{if(MenuInputEnabled&&Catalog.Stages.Count>0)_generator.Call("DeleteMap",Catalog.Stages[SelectedIndex].SongId);};
    }
    private SongPreview _preview = null!;
    private bool _leaving;
    private Tween? _tween, _environmentTween, _rippleTween;
    public override void _Ready()
    {
        _preview=GetNode<SongPreview>("SongPreview");_mainCatalog=Catalog;_cardsOrigin=Cards.Position;
        GetNode<Button>("Categories/Main").Pressed+=()=>Category(false);
        var customButton=GetNode<Button>("Categories/Custom");customButton.Visible=Gamejam2.Data.CustomMapRegistry.Maps.Count>0;customButton.Pressed+=()=>Category(true);
        var cards = Cards.GetChildren().OfType<StageCard>().ToList();
        while (cards.Count < Catalog.Stages.Count)
        { var card = CardScene.Instantiate<StageCard>(); Cards.AddChild(card); cards.Add(card); }
        foreach (var card in cards) card.Selected += CardPressed;
        TitleButton.Pressed += Back;
        LeftArrow.Pressed += () => Navigate(-1); RightArrow.Pressed += () => Navigate(1);
        InstallGenerator();
        ProgressService.Changed += Refresh; Refresh(); Arrange(false);
    }
    public void Refresh()
    {
        if(_addMusic!=null){_addMusic.Visible=CustomCategory;_deleteMusic!.Visible=CustomCategory&&Catalog.Stages.Count>0&&Gamejam2.Data.CustomMapRegistry.Find(Catalog.Stages[SelectedIndex].SongId) is {Generated:true};}
        while(Cards.GetChildren().OfType<StageCard>().Count()<Catalog.Stages.Count){var card=CardScene.Instantiate<StageCard>();Cards.AddChild(card);card.Selected+=CardPressed;}
        var cards = Cards.GetChildren().OfType<StageCard>().ToArray();
        for (int index = 0; index < cards.Length; index++)
        { cards[index].Visible = index < Catalog.Stages.Count; if (index < Catalog.Stages.Count) cards[index].Bind(Catalog.Stages[index], Available(Catalog.Stages[index].StageId)); }
        Arrange(false); TransitionEnvironment(false, 0); if(Catalog.Stages.Count>0)_preview.Select(Catalog.Stages[SelectedIndex].SongId);
    }
    /// <summary>화면 밖에서 wrap하지 않는다. 연타는 현재 tween의 pose에서 새 목표로 이동한다.</summary>
    public void Navigate(int direction)
    {
        int next = Math.Clamp(SelectedIndex + direction, 0, Math.Max(0, Catalog.Stages.Count - 1));
        if (!MenuInputEnabled || next == SelectedIndex) return;
        int movement = Math.Sign(next - SelectedIndex);
        SelectedIndex = next; if(_deleteMusic!=null)_deleteMusic.Visible=CustomCategory&&Gamejam2.Data.CustomMapRegistry.Find(Catalog.Stages[SelectedIndex].SongId) is {Generated:true}; Arrange(true); TransitionEnvironment(true, movement);_preview.Select(Catalog.Stages[SelectedIndex].SongId);RestoreCardFocus();
    }
    private void CardPressed(string id)
    {
        int index = Catalog.Stages.ToList().FindIndex(data => data.StageId == id);
        if (!MenuInputEnabled || index < 0) return;
        if (index == SelectedIndex) SelectCurrent(); else Navigate(index - SelectedIndex);
    }
    public void SelectCurrent()
    {
        if (!MenuInputEnabled || Catalog.Stages.Count == 0) return;
        var data = Catalog.Stages[SelectedIndex];
        if (data.IsAvailable && Available(data.StageId)) Leave(()=>StageSelected?.Invoke(data.StageId));
    }
    private void Arrange(bool animate)
    {
        _tween?.Kill();
        if (animate) _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        var cards = Cards.GetChildren().OfType<StageCard>().ToArray();
        for (int i = 0; i < cards.Length && i < Catalog.Stages.Count; i++)
        {
            var card = cards[i]; bool selected = i == SelectedIndex;
            // Shared scene reference pose, scaled by the same root viewport stretch at every resolution.
            Vector2 target = FocusAnchor.Position + new Vector2((i - SelectedIndex) * CardSpacing, 0);
            Vector2 scale = Vector2.One * (selected ? 1 : SideScale);
            card.ZIndex = selected ? 2 : 0; card.SetSelected(selected, animate);
            if (!animate) { card.Position = target; card.Scale = scale; }
            else { _tween!.TweenProperty(card, "position", target, TransitionSeconds); _tween.TweenProperty(card, "scale", scale, TransitionSeconds); }
        }
        UpdateInteraction();
        PositionLabel.Text = $"{SelectedIndex + 1} / {Catalog.Stages.Count}";
        if (Catalog.Stages.Count == 0) return;
        var data = Catalog.Stages[SelectedIndex];
        Information.Text = !data.IsAvailable?"준비 중인 곡입니다":Available(data.StageId)
            ? "Enter / Space 또는 카드를 눌러 시작" : "아직 잠겨 있는 곡입니다";
    }
    private void UpdateInteraction()
    {
        LeftArrow.Disabled = !MenuInputEnabled || SelectedIndex == 0;
        RightArrow.Disabled = !MenuInputEnabled || SelectedIndex >= Catalog.Stages.Count - 1;
        LeftArrow.Refresh(); RightArrow.Refresh();
        TitleButton.Disabled = !MenuInputEnabled;
        GetNode<Button>("Categories/Main").Disabled=!MenuInputEnabled;
        GetNode<Button>("Categories/Custom").Disabled=!MenuInputEnabled;
        foreach (var card in Cards.GetChildren().OfType<StageCard>()) card.SetInteractionEnabled(MenuInputEnabled);
    }
    private void RestoreCardFocus()
    {
        if (!IsInsideTree() || !MenuInputEnabled) return;
        var card = Cards.GetChildren().OfType<StageCard>().ElementAtOrDefault(SelectedIndex);
        if (card != null && !card.SelectButton.Disabled) card.SelectButton.GrabFocus();
        else if (!RightArrow.Disabled) RightArrow.GrabFocus();
        else if (!LeftArrow.Disabled) LeftArrow.GrabFocus();
    }
    private void TransitionEnvironment(bool animate, int direction)
    {
        _environmentTween?.Kill();
        if (animate) _environmentTween = CreateTween().SetParallel();
        var layers = EnvironmentLayers.GetChildren().OfType<Control>().ToArray();
        for (int i = 0; i < layers.Length; i++)
        {
            if (i < Catalog.Stages.Count && layers[i] is TextureRect texture) texture.Texture = Catalog.Stages[i].BackgroundTexture;
            Color tint = new(1, 1, 1, i == SelectedIndex ? BackdropOpacity : 0);
            if (animate) _environmentTween!.TweenProperty(layers[i], "modulate", tint, TransitionSeconds);
            else layers[i].Modulate = tint;
        }
        // These three authored ambience presets are presentation previews, not song/gameplay rules.
        var emitters = Ambience.GetChildren().OfType<CpuParticles2D>().ToArray();
        for (int i = 0; i < emitters.Length; i++) emitters[i].Emitting = i == SelectedIndex;
        if (!animate) return;
        _rippleTween?.Kill();
        TransitionMaterial.SetShaderParameter("direction", direction);
        TransitionMaterial.SetShaderParameter("progress", 0.0);
        _rippleTween = CreateTween();
        _rippleTween.TweenProperty(TransitionMaterial, "shader_parameter/progress", 1.0, TransitionSeconds);
        TransitionBubbles.Position = FocusAnchor.Position + new Vector2(direction > 0 ? 1110 : 10, 555);
        TransitionBubbles.Restart(); TransitionBubbles.Emitting = true;
    }
    private void Leave(Action action){if(_leaving)return;_leaving=true;MenuInputEnabled=false;_preview.FadeOut(action);}
    private void Back() => Leave(()=>TitleRequested?.Invoke());
    public override void _Input(InputEvent e)
    {
        if(_generator!=null&&_generator.Call("IsOpen").AsBool())return;
        if (!MenuInputEnabled || e.IsEcho()) return;
        var viewport = GetViewport();
        if(e is InputEventKey {Pressed:true} key&&(key.PhysicalKeycode is Key.Q or Key.E||key.Keycode is Key.Q or Key.E))
        {KeyboardAction?.Invoke();Category(!CustomCategory);viewport.SetInputAsHandled();return;}
        if (e.IsActionPressed("stage_previous")) {if(SelectedIndex>0)KeyboardAction?.Invoke();Navigate(-1);}
        else if (e.IsActionPressed("stage_next")) {if(SelectedIndex<Catalog.Stages.Count-1)KeyboardAction?.Invoke();Navigate(1);}
        else if (e.IsActionPressed("stage_select"))
        {
            KeyboardAction?.Invoke();
            if(_addMusic is {Visible:true}&&_addMusic.HasFocus()){_generator!.Call("Open");viewport.SetInputAsHandled();return;}
            if(_deleteMusic is {Visible:true}&&_deleteMusic.HasFocus()){_generator!.Call("DeleteMap",Catalog.Stages[SelectedIndex].SongId);viewport.SetInputAsHandled();return;}
            if (TitleButton.HasFocus()) Back();
            else if(GetNode<Button>("Categories/Main").HasFocus())Category(false);
            else if(GetNode<Button>("Categories/Custom").HasFocus())Category(true);
            else SelectCurrent();
        }
        else if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("stage_back")) {KeyboardAction?.Invoke();Back();}
        else return;
        viewport.SetInputAsHandled();
    }
    public override void _ExitTree() { _categoryTween?.Kill();_tween?.Kill(); _environmentTween?.Kill(); _rippleTween?.Kill(); ProgressService.Changed -= Refresh; }
}
