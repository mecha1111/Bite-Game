using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Fixed screen layer. Owns no schedule/clock; observer of real interference.</summary>
public partial class InterferenceIndicators : CanvasLayer
{
    private GameplayScreen _screen=null!;
    private InterferenceBubble[] _bubbles=Array.Empty<InterferenceBubble>();
    private bool _subscribed;
    private Control? _tutorialMessage;
    private readonly Dictionary<string,Control> _safeZones=new();
    private int _activeMask=-1;
    public string SlotFor(string type)=>type switch {"ship_horn"=>"TopLeftInterferenceSafe/Primary","fishing_float"=>"TopLeftInterferenceSafe/"+(_bubbles.Any(b=>b.EffectType=="ship_horn"&&b.Active)?"Secondary":"Primary"),"whale"=>"RightSafe",_=>"LeftSafe"};
    private Viewport? _viewport;
    private readonly HashSet<InterferenceBubble> _placed=new();
    public void Initialize(GameplayScreen screen)
    {
        if(_screen==screen){foreach(var bubble in _bubbles)bubble.Reset();Layout();return;}
        _screen=screen;_bubbles=GetNode<Control>("SafeArea").GetChildren().OfType<InterferenceBubble>().ToArray();
        _tutorialMessage=screen.GetNodeOrNull<Control>("TutorialHud/MessageArea");
        foreach(string slot in new[]{"TopLeftInterferenceSafe/Primary","TopLeftInterferenceSafe/Secondary","LeftSafe","RightSafe"})_safeZones[slot]=GetNode<Control>("SafeZones/"+slot);
        Connect();Layout();
    }
    private void Connect()
    {
        if(_subscribed||_screen==null)return;
        _screen.Interference.Started+=Started;_screen.Interference.Emitted+=Emitted;
        _viewport=_screen.GetViewport();_viewport.SizeChanged+=Layout;_subscribed=true;
    }
    public override void _EnterTree(){if(_screen!=null){Connect();Layout();}}
    private void Started(string type,double time){foreach(var bubble in _bubbles)if(bubble.EffectType==type)bubble.Pop(time);}
    private void Emitted(string type,Vector2 origin,double time){if(type is "fishing_float" or "ship_horn")foreach(var b in _bubbles)if(b.EffectType==type)b.Pulse(time);}
    private void Layout()
    {
        if(_screen==null)return;
        _placed.Clear();
        var occupied=new List<Rect2>{_screen.Gauge.GetGlobalRect().Grow(8),_screen.SettingsButton.GetGlobalRect().Grow(8),_screen.Combo.GetGlobalRect().Grow(24),new Rect2(_screen.Feedback.GlobalPosition-new Vector2(250,80),new Vector2(500,160)).Grow(8)};
        var message=_tutorialMessage;if(message!=null)occupied.Add(message.GetGlobalRect().Grow(8));
        // Shared top-left slots and side anchors reserve the full pop envelope.
        foreach(string type in new[]{"ship_horn","fishing_float","sardine","whale"})
        {
            var b=_bubbles.Single(x=>x.EffectType==type);if(!b.Active)continue;var zone=_safeZones[SlotFor(type)].GetGlobalRect();
            var half=b.Size*.56f;var min=zone.Position+half;var max=zone.End-half;
            if(min.X>max.X||min.Y>max.Y)continue;
            float edge=32;
            var preferred=new Vector2(type=="whale"?zone.End.X-edge-half.X:zone.Position.X+edge+half.X,type is "ship_horn" or "fishing_float"?zone.Position.Y+edge+half.Y:zone.GetCenter().Y).Clamp(min,max);
            var xs=new List<float>{min.X,max.X,preferred.X};var ys=new List<float>{min.Y,max.Y,preferred.Y};
            foreach(var obstacle in occupied){xs.Add(Math.Clamp(obstacle.Position.X-half.X-1,min.X,max.X));xs.Add(Math.Clamp(obstacle.End.X+half.X+1,min.X,max.X));ys.Add(Math.Clamp(obstacle.Position.Y-half.Y-1,min.Y,max.Y));ys.Add(Math.Clamp(obstacle.End.Y+half.Y+1,min.Y,max.Y));}
            float nearest=float.PositiveInfinity;Vector2 chosen=Vector2.Zero;
            foreach(float x in xs)foreach(float y in ys)
            {
                var center=new Vector2(x,y).Round();var rect=new Rect2(center-half,half*2);
                if(!zone.Encloses(rect)||occupied.Any(o=>o.Intersects(rect)))continue;
                float distance=center.DistanceSquaredTo(preferred);if(distance<nearest){nearest=distance;chosen=center;}
            }
            if(!float.IsFinite(nearest))continue;
            b.Position=(chosen-b.Size/2).Round();_placed.Add(b);occupied.Add(new Rect2(chosen-half,half*2).Grow(8));
        }
    }
    public void Present(double time,bool blocked)
    {
        int mask=0;for(int i=0;i<_bubbles.Length;i++)if(_bubbles[i].Active)mask|=1<<i;
        if(mask!=_activeMask){_activeMask=mask;Layout();}
        var gauge=_screen.Gauge.GetGlobalRect();var settings=_screen.SettingsButton.GetGlobalRect();var combo=_screen.Combo.GetGlobalRect();
        var judgment=new Rect2(_screen.Feedback.GlobalPosition-new Vector2(250,80),new Vector2(500,160));
        foreach(var b in _bubbles)
        {
            if(!b.Active)continue;
            // Reserve critical HUD rectangles, including optional tutorial message controls.
            var rect=new Rect2(b.Position-b.Size*.06f,b.Size*1.12f);
            bool collision=rect.Intersects(gauge)||rect.Intersects(settings)||rect.Intersects(combo)||rect.Intersects(judgment);
            if(_screen is Gamejam2.Tutorial.TutorialGameplayScreen){var message=_tutorialMessage;if(message!=null&&message.Visible)collision|=rect.Intersects(message.GetGlobalRect());}
            b.Present(time,_screen.Interference.GetEffect(b.EffectType).IsActive,blocked||collision||!_placed.Contains(b));
        }
    }
    public void Stop(){foreach(var bubble in _bubbles)bubble.Reset();_activeMask=-1;_placed.Clear();}
    public override void _ExitTree()
    {
        if(!_subscribed)return;_screen.Interference.Started-=Started;_screen.Interference.Emitted-=Emitted;
        if(_viewport!=null)_viewport.SizeChanged-=Layout;_viewport=null;_subscribed=false;
    }
}
