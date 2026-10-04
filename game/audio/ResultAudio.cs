using System;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Audio;
public partial class ResultAudio : Node
{
    [Export] public AudioStreamPlayer Entrance { get; set; } = null!;
    [Export] public AudioStreamPlayer Fill { get; set; } = null!;
    private Dictionary<string,SfxClip> _clips=null!;
    public int Entrances { get; private set; }
    public int Fills { get; private set; }
    public override void _Ready()=>_clips=SfxCatalog.Load();
    public void Begin(){Stop();Entrances++;SfxCatalog.Play(Entrance,_clips["result_jingle"]);}
    public void BeginFill(){Fills++;SfxCatalog.Play(Fill,_clips["result_gauge_fill"]);}
    public void FillProgress(double phase)
    {
        if(phase>=1){Fill.Stop();return;}
        float fade=(float)Math.Clamp((1-phase)/.15,0,1);
        Fill.VolumeDb=_clips["result_gauge_fill"].VolumeDb+Mathf.LinearToDb(Math.Max(.0001f,fade));
    }
    public void StopFill()=>Fill.Stop();
    public void Stop(){Entrance.Stop();Fill.Stop();}
}
