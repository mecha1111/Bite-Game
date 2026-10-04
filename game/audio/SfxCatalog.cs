using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Audio;
public sealed record SfxClip(AudioStreamWav Stream,float[] Left,float[] Right,float VolumeDb,float Pitch,float Stereo,string Mode,double MaxDuration,double FadeOut)
{
    public double Duration=>MaxDuration>0?Math.Min(MaxDuration,Left.Length/(48000d*Pitch)):Left.Length/(48000d*Pitch);
    public float Fade(double age)=>FadeOut>0?(float)Math.Clamp((Duration-age)/FadeOut,0,1):1;
}
/// <summary>Processed pack gains are the mix source; gameplay CSV adds editable pitch/pan/lifetime and gain trims.</summary>
public static class SfxCatalog
{
    private static readonly Dictionary<string,(AudioStreamWav Stream,float[] Left,float[] Right)> Pcm=new();
    public static void ClearCache(){foreach(var pcm in Pcm.Values)pcm.Stream.Dispose();Pcm.Clear();}
    public static Dictionary<string,SfxClip> Load()
    {
        var reference=LocalCsv.Read("res://data/audio/sfx_mix_recommendations.csv").ToDictionary(r=>r["file"],r=>(float)LocalCsv.Number(r["runtime_volume_db"]));
        return LocalCsv.Read("res://data/balance/gameplay_sfx.csv").ToDictionary(r=>r["sfx_id"],r=>
        {
            string path=r["sfx_path"];
            if(!Pcm.TryGetValue(path,out var pcm))
            {
                var wav=GD.Load<AudioStreamWav>(path)??throw new ArgumentException("SFX missing: "+path);
                if(wav.Format!=AudioStreamWav.FormatEnum.Format16Bits||wav.MixRate!=48000)throw new ArgumentException("SFX requires uncompressed 48kHz PCM16: "+path);
                byte[] bytes=wav.Data;int channels=wav.Stereo?2:1;var left=new float[bytes.Length/2/channels];var right=wav.Stereo?new float[left.Length]:left;
                for(int i=0;i<left.Length;i++){int p=i*2*channels;left[i]=(short)(bytes[p]|bytes[p+1]<<8)/32768f;if(channels==2)right[i]=(short)(bytes[p+2]|bytes[p+3]<<8)/32768f;}
                pcm=(wav,left,right);Pcm.Add(path,pcm);
            }
            float db=(float)LocalCsv.Number(r["volume_db"])+(reference.TryGetValue(path[(path.LastIndexOf('/')+1)..],out float recommended)?recommended:0);
            float pitch=(float)LocalCsv.Number(r["pitch_scale"]),pan=(float)LocalCsv.Number(r["stereo_strength"]);
            double duration=LocalCsv.Number(r["max_duration_sec"]),fade=LocalCsv.Number(r["fade_out_sec"]);
            if(pitch<=0||pan<0||pan>.4||db>0||duration<0||fade<0)throw new ArgumentException("SFX config range: "+path);
            if(OS.GetCmdlineUserArgs().Contains("--runtime-audit"))GD.Print($"[SFX SOURCE] id={r["sfx_id"]} stream={pcm.Stream.ResourcePath} bus=SFX dB={db} pitch={pitch} duration={duration}");
            return new SfxClip(pcm.Stream,pcm.Left,pcm.Right,db,pitch,pan,r["mode"],duration,fade);
        });
    }
    public static void Play(AudioStreamPlayer player,SfxClip clip,float trim=0)
    {player.Stream=clip.Stream;player.Bus="SFX";player.VolumeDb=clip.VolumeDb+trim;player.PitchScale=clip.Pitch;player.Play();}
}
