using System;
using System.Linq;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>One implementation for mirrored lanes. Slot origins are authored in Gameplay.tscn, never repositioned.</summary>
public partial class RhythmSignalLane : Control
{
    public RhythmSignalSlot[] Slots { get; private set; } = Array.Empty<RhythmSignalSlot>();
    public override void _Ready()
    { Slots=GetChildren().OfType<RhythmSignalSlot>().ToArray();if(Slots.Length!=10)throw new InvalidOperationException("리듬 lane은 10개 slot이 필요합니다."); }
    public void Activate(int slot,WaveKind kind,int count,double time,int encounterId=-1,Color? tint=null,string waveEventId="") => Slots[slot].Activate(kind,count,time,encounterId,tint,waveEventId);
    public void Present(double time) {foreach(var slot in Slots) slot.Present(time);}
    public void Reset() {foreach(var slot in Slots) slot.Reset();}
}
