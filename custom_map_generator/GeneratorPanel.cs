using System;
using Godot;
using Gamejam2.Save;
namespace Gamejam2.CustomGenerator;
public partial class GeneratorPanel : CanvasLayer
{
    [Signal] public delegate void MapsChangedEventHandler();
    private Control _root=null!;private VBoxContainer _content=null!;private FileDialog _picker=null!;private bool _busy;
    private Font _font=null!;
    public override void _Ready()
    {
        Layer=30;ProcessMode=ProcessModeEnum.Always;_font=GD.Load<Font>("res://assets/fonts/neodgm.ttf");
        _root=new Control{Name="LocalGenerator",Visible=false};AddChild(_root);_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var veil=new ColorRect{Color=new Color(.005f,.025f,.055f,.97f)};_root.AddChild(veil);veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel=new MarginContainer();_root.AddChild(panel);panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);panel.OffsetLeft=-660;panel.OffsetRight=660;panel.OffsetTop=-460;panel.OffsetBottom=460;
        _content=new VBoxContainer();_content.AddThemeConstantOverride("separation",16);panel.AddChild(_content);
        _picker=new FileDialog{Access=FileDialog.AccessEnum.Filesystem,FileMode=FileDialog.FileModeEnum.OpenFile,UseNativeDialog=true,Title="내 음악 추가",Filters=new[]{"*.mp3 ; MP3"}};AddChild(_picker);
        _picker.FileSelected+=Selected;_picker.Canceled+=()=>_root.Hide();
    }
    Label Label(string text,int size=28){var label=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart};label.AddThemeFontOverride("font",_font);label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",new Color(.65f,1,1));_content.AddChild(label);return label;}
    Button Button(string text,Action action){var button=new Button{Text=text,CustomMinimumSize=new Vector2(0,58)};button.AddThemeFontOverride("font",_font);button.AddThemeFontSizeOverride("font_size",28);button.AddThemeColorOverride("font_color",new Color(.65f,1,1));var style=new StyleBoxFlat{BgColor=new Color(.025f,.12f,.17f),BorderColor=new Color(.15f,.5f,.6f)};style.SetBorderWidthAll(2);button.AddThemeStyleboxOverride("normal",style);button.Pressed+=action;_content.AddChild(button);return button;}
    void Clear(){foreach(Node child in _content.GetChildren()){_content.RemoveChild(child);child.QueueFree();}}
    public void Open()
    {
        if(_busy)return;_root.Show();Clear();
        if(SaveStore.Read("local_generator","rights_acknowledged",false).AsBool()){_picker.PopupCenteredRatio(.7f);return;}
        Label("BITE 커스텀 맵 이용 안내",36);Label(Godot.FileAccess.GetFileAsString("res://custom_map_generator/rights_notice.txt").Replace("BITE 커스텀 맵 이용 안내\n", ""),24);
        var check=new CheckBox{Text="위 내용을 확인했으며, 사용하려는 음악에 필요한 권리를 확인했습니다.",ButtonPressed=false};check.AddThemeFontOverride("font",_font);check.AddThemeFontSizeOverride("font_size",24);_content.AddChild(check);
        var agree=Button("동의하고 계속",()=>{SaveStore.Write("local_generator","rights_acknowledged",true);SaveStore.Flush();Clear();_picker.PopupCenteredRatio(.7f);});agree.Disabled=true;check.Toggled+=value=>agree.Disabled=!value;
        Button("취소",()=>_root.Hide());
    }
    private async void Selected(string path)
    {
        if(_busy)return;_busy=true;Clear();Label("내 음악 추가",36);var progress=Label("음악 분석 중",32);
        try{await LocalMapGenerator.Generate(this,path,text=>progress.Text=text);EmitSignal(SignalName.MapsChanged);Label("로컬 맵이 추가되었습니다.");Button("완료",()=>_root.Hide());}
        catch(Exception e){GD.PushError("[LOCAL MAP] generation failed: "+e.GetType().Name);Label("생성하지 못했습니다. "+(e is InvalidOperationException?e.Message:"다른 MP3를 선택해 주세요."));Button("닫기",()=>_root.Hide());}
        finally{_busy=false;}
    }
    public void DeleteMap(string id)
    {
        if(_busy)return;_root.Show();Clear();Label("이 로컬 맵을 삭제할까요?",36);Label("생성된 리듬과 연출, 로컬 음원·커버 캐시가 삭제됩니다.");
        Button("삭제",()=>{LocalMapGenerator.Delete(id);EmitSignal(SignalName.MapsChanged);_root.Hide();});Button("취소",()=>_root.Hide());
    }
    public override void _Input(InputEvent input){if(_root.Visible&&input.IsActionPressed("ui_cancel")&&!_busy){_root.Hide();GetViewport().SetInputAsHandled();}}
    public bool IsOpen()=>_root.Visible;
}
