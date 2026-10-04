using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Debugging;
/// <summary>Offline developer analysis of current-source MP3 PCM. Does not run a gameplay clock.</summary>
public partial class MusicAccentAnalysis : Node
{
    public override void _Ready()
    {
        try
        {
            int rate=(int)AudioServer.GetMixRate(),bin=rate/200;
            bool v2=OS.GetCmdlineUserArgs().Contains("--v2-sources");
            string[] files=Gamejam2.Data.LocalCsv.Read("res://data/balance/song_timing.csv").Select(r=>r["audio_path"]).ToArray();
            for(int song=0;song<4;song++)
            {
                var stream=Gamejam2.Audio.SongAudio.Load(files[song]);
                using var playback=stream.InstantiatePlayback();playback.Start();
                var rows=new List<string>{"time_sec,rms,low_energy,mid_energy,high_energy,onset_strength"};
                double lo=0,mid=0,prevLo=0,prevMid=0,prevHi=0,lowEnergy=0,midEnergy=0,hiEnergy=0,energy=0;int count=0;long frame=0;
                double aLo=1-Math.Exp(-2*Math.PI*180/rate),aMid=1-Math.Exp(-2*Math.PI*2200/rate);
                while(playback.GetPlaybackPosition()<stream.GetLength()-.001)
                {
                    var samples=playback.MixAudio(1,4096);if(samples.Length==0)break;
                    foreach(var pair in samples)
                    {
                        double x=(pair.X+pair.Y)*.5;lo+=aLo*(x-lo);mid+=aMid*(x-mid);
                        lowEnergy+=lo*lo;midEnergy+=(mid-lo)*(mid-lo);hiEnergy+=(x-mid)*(x-mid);energy+=x*x;count++;frame++;
                        if(count!=bin)continue;
                        double l=Math.Sqrt(lowEnergy/bin),m=Math.Sqrt(midEnergy/bin),h=Math.Sqrt(hiEnergy/bin),r=Math.Sqrt(energy/bin);
                        double onset=Math.Max(0,l-prevLo)*1.8+Math.Max(0,m-prevMid)+Math.Max(0,h-prevHi)*.8;
                        rows.Add(FormattableString.Invariant($"{(double)(frame-bin/2)/rate:F6},{r:F7},{l:F7},{m:F7},{h:F7},{onset:F7}"));
                        prevLo=l;prevMid=m;prevHi=h;lowEnergy=midEnergy=hiEnergy=energy=0;count=0;
                    }
                    if(frame>rate*(stream.GetLength()+1))break;
                }
                System.IO.File.WriteAllLines($"/private/tmp/bite-{(v2?"v2-":"")}song{song+1}-accents.csv",rows);
                GD.Print($"ANALYZED song_{song+1}: {files[song]}, {stream.GetLength():F6}s, {rows.Count-1} bins, {rate} Hz");
            }
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
