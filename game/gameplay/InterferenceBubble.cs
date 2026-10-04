using System;
using Godot;
namespace Gamejam2.Gameplay;
public partial class InterferenceBubble : Control
{
    [Export] public string EffectType {get;set;}="";
    [Export] public float TargetVisualHeight {get;set;}=192;
    [Export] public bool Mirror {get;set;}
    [Export] public TextureRect Still {get;set;}=null!;
    [Export] public AnimatedSprite2D Animation {get;set;}=null!;
    [Export] public SpriteFrames? Frames {get;set;}
    [Export] public Texture2D? StillTexture {get;set;}
    private StringName _activeAnimation="";
    private bool _initialized;
    private double[] _frameDurations=Array.Empty<double>();
    private double _cycle;
    public bool Initialized=>_initialized;
    public string SelectedAnimation=>_activeAnimation.ToString();
    private double _pop=double.NegativeInfinity,_end=double.NegativeInfinity;
    private double _pulse=double.NegativeInfinity;
    public bool Active {get;private set;}
    public override void _Ready()
    {
        SetProcess(false);Visible=false;_initialized=false;
        if(Animation==null||Still==null){Fail("required Animation/Still node missing");return;}
        if(Frames!=null)Animation.SpriteFrames=Frames;
        if(StillTexture!=null)Still.Texture=StillTexture;
        var frames=Animation.SpriteFrames;
        if(frames==null){Fail("SpriteFrames missing");return;}
        // Prefer the existing authored animation, never assume a name or an empty default.
        string selected="";
        foreach(string name in frames.GetAnimationNames())
            if(frames.GetFrameCount(name)>0){selected=name;if(name=="active")break;}
        if(selected.Length==0){Fail("SpriteFrames has no non-empty animation: "+frames.ResourcePath);return;}
        _activeAnimation=selected;
        var texture=frames.GetFrameTexture(_activeAnimation,0);
        if(texture==null||texture.GetHeight()<=0||TargetVisualHeight<=0||Still.Texture==null)
        {Fail("first frame/static texture missing or invalid: "+frames.ResourcePath);return;}
        double speed=frames.GetAnimationSpeed(_activeAnimation);
        if(speed<=0){Fail("invalid animation speed: "+frames.ResourcePath);return;}
        _frameDurations=new double[frames.GetFrameCount(_activeAnimation)];_cycle=0;
        for(int i=0;i<_frameDurations.Length;i++)
        {
            if(frames.GetFrameTexture(_activeAnimation,i)==null){Fail("frame texture missing: "+frames.ResourcePath+" frame "+i);return;}
            _frameDurations[i]=frames.GetFrameDuration(_activeAnimation,i)/speed;_cycle+=_frameDurations[i];
        }
        if(_cycle<=0){Fail("empty animation duration: "+frames.ResourcePath);return;}
        Animation.Animation=_activeAnimation;
        float factor=TargetVisualHeight/texture.GetHeight();Size=new Vector2(Mathf.Ceil(texture.GetWidth()*factor),TargetVisualHeight);
        Animation.Scale=Vector2.One*factor;Animation.Position=Size/2;Animation.FlipH=Still.FlipH=Mirror;
        Animation.Stop();PivotOffset=Size/2;_initialized=true;
    }
    private void Fail(string reason)
    {
        Active=false;Visible=false;
        GD.PushError("InterferenceBubble ["+EffectType+"] "+GetPath()+": "+reason);
    }
    public void Pop(double time){if(!_initialized)return;_pop=time;_end=_pulse=double.NegativeInfinity;Active=true;}
    public void Pulse(double time){if(Active&&time-_pop>=.24)_pulse=time;}
    public void Reset(){Active=false;Visible=false;_pop=_end=_pulse=double.NegativeInfinity;}
    public void Present(double time,bool active,bool blocked)
    {
        if(!_initialized||!Active){Visible=false;return;}
        if(Active&&!active&&double.IsNegativeInfinity(_end))_end=time;
        if(!double.IsNegativeInfinity(_end)&&time-_end>=.18)Active=false;
        Visible=Active&&!blocked;Animation.Stop();if(!Visible)return;
        double age=time-_pop;float scale=age<.14?Mathf.Lerp(.35f,1.12f,(float)Math.Clamp(age/.14,0,1)):Mathf.Lerp(1.12f,1,(float)Math.Clamp((age-.14)/.10,0,1));
        double pulseAge=time-_pulse;if(pulseAge>=0&&pulseAge<.16)scale*=1+.08f*Mathf.Sin((float)(pulseAge/.16*Math.PI));
        float fade=double.IsNegativeInfinity(_end)?1:1-(float)Math.Clamp((time-_end)/.18,0,1);
        Scale=Vector2.One*scale*(.9f+.1f*fade);Modulate=new Color(1,1,1,fade);
        Still.Visible=age<.24;Animation.Visible=!Still.Visible;
        // Advance supplied GIF frames from presentation time; pause/offset never desynchronizes an independent animation timer.
        double at=Math.Max(0,age-.24)%_cycle;
        for(int i=0;i<_frameDurations.Length;i++){if(at<_frameDurations[i]){Animation.Frame=i;break;}at-=_frameDurations[i];}
    }
}
