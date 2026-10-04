using System;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Audio;
/// <summary>Persistent quiet SFX-bus UI feedback. Visual UI remains in its authored scenes.</summary>
public partial class UiAudio : Node
{
    [Export] public AudioStreamPlayer Player { get; set; } = null!;
    private SfxClip _click=null!;
    private readonly HashSet<ulong> _bound=new();
    public int ClickCount { get; private set; }
    public int BindingCount=>_bound.Count;
    public override void _Ready(){_click=SfxCatalog.Load()["ui_click"];GetTree().NodeAdded+=Added;GetTree().NodeRemoved+=Removed;Bind(GetTree().Root);}
    private void Added(Node node){if(node is BaseButton||node is Slider||node is Gamejam2.Lobby.LobbyScreen)Callable.From(()=>Bind(node)).CallDeferred();}
    private void Removed(Node node)
    {
        ulong id=node.GetInstanceId();if(!_bound.Contains(id))return;
        // Router may detach live Gameplay for Calibration; keep that binding.
        // After deferred deletion, retain no IDs for freed buttons/sliders/screens.
        Callable.From(()=>
        {
            var instance=GodotObject.InstanceFromId(id);
            if(!GodotObject.IsInstanceValid(instance)){_bound.Remove(id);return;}
            for(var owner=instance as Node;owner!=null;owner=owner.GetParent())
                if(owner.IsQueuedForDeletion()){_bound.Remove(id);break;}
        }).CallDeferred();
    }
    private void Bind(Node node)
    {
        if(!GodotObject.IsInstanceValid(node)||!node.IsInsideTree())return;
        if(node is Gamejam2.Lobby.LobbyScreen lobby&&_bound.Add(node.GetInstanceId()))lobby.KeyboardAction+=Click;
        if(node is BaseButton button&&_bound.Add(node.GetInstanceId()))
        {button.Pressed+=Click;if(button is OptionButton option)option.ItemSelected+=_=>Click();}
        if(node is Slider slider&&_bound.Add(node.GetInstanceId()))slider.DragStarted+=Click;
        foreach(var child in node.GetChildren())Bind(child);
    }
    public void Click(){ClickCount++;SfxCatalog.Play(Player,_click);}
    public override void _ExitTree(){GetTree().NodeAdded-=Added;GetTree().NodeRemoved-=Removed;_bound.Clear();SfxCatalog.ClearCache();}
}
