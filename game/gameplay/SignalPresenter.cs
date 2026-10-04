using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Chart-derived activation cursor. Only presentation uses VisualOffset; input targets remain unchanged.</summary>
public partial class SignalPresenter : Node
{
    [Export] public RhythmSignalLane LeftLane { get; set; } = null!;
    [Export] public RhythmSignalLane RightLane { get; set; } = null!;
    private (bool Left,int Slot,WaveKind Kind,int Count,double Time,int Prey,string WaveId)[] _activations=Array.Empty<(bool,int,WaveKind,int,double,int,string)>();
    public Func<int,int,Color>? Guidance { get; set; }
    public bool ShowResolvedTargets { get; set; }
    private int _next;
    private readonly HashSet<int> _resolved=new();
    private readonly HashSet<int> _skipped=new();
    public void Initialize(GameplayData data)
    {
        LeftLane.Reset();RightLane.Reset();_next=0;_resolved.Clear();_skipped.Clear();
        _activations=data.Phrases.SelectMany(p=>p.Cues.Select((time,index)=>(p.FromLeft,p.CueSlots[index],p.WaveKind,p.PulseCount,time,p.Index,""))
            .Concat(p.TargetEvents.Where(t=>double.IsFinite(t.CueTime)).Select(t=>(p.FromLeft,0,p.WaveKind,1,t.CueTime,p.Index,t.WaveEventId+":cue")))
            .Concat(p.TargetEvents.Select(t=>(p.FromLeft,p.TargetSlot,p.WaveKind,t.WaveEventId.Length>0?1:p.PulseCount,t.TargetTime,p.Index,t.WaveEventId)))).OrderBy(a=>a.Item5).ToArray();
    }
    public void ResolveEncounter(int index,double time)=>_resolved.Add(index); // Existing rings finish naturally; never gate the next cue.
    internal void SkipEncounter(int index)=>_skipped.Add(index);
    public void Present(double time)
    {
        while(_next<_activations.Length && _activations[_next].Time<=time)
        {var a=_activations[_next++];if(!_skipped.Contains(a.Prey)&&(!_resolved.Contains(a.Prey)||(a.WaveId.Length>0||(a.Slot==8&&ShowResolvedTargets))))(a.Left?LeftLane:RightLane).Activate(a.Slot,a.Kind,a.Count,a.Time,a.Prey,Guidance?.Invoke(a.Prey,a.Slot),a.WaveId);}
        LeftLane.Present(time);RightLane.Present(time);
    }
}
