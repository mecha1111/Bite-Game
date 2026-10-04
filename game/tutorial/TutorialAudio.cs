using System;
using Godot;
namespace Gamejam2.Tutorial;
/// <summary>The supplied MP3 remains the source. Decode before the first playback request so
/// phrase-boundary retries do not synchronously scan MP3 frames on the rhythm/render thread.</summary>
public static class TutorialAudio
{
    public static AudioStreamWav Prepare(AudioStreamMP3 source)
    {
        int rate=(int)AudioServer.GetMixRate();int count=(int)Math.Ceiling(source.GetLength()*rate);
        byte[] pcm=new byte[checked(count*4)];
        using var playback=source.InstantiatePlayback();playback.Start();
        for(int frame=0;frame<count;)
        {
            int block=Math.Min(8192,count-frame);var samples=playback.MixAudio(1,block);
            // The final decoded MP3 block may contain fewer samples than requested.
            int available=Math.Min(block,samples.Length);
            for(int i=0;i<available;i++)
            {
                short left=(short)Math.Round(Math.Clamp(samples[i].X,-1,1)*32767),right=(short)Math.Round(Math.Clamp(samples[i].Y,-1,1)*32767);
                int at=(frame+i)*4;pcm[at]=(byte)(left&255);pcm[at+1]=(byte)((left>>8)&255);pcm[at+2]=(byte)(right&255);pcm[at+3]=(byte)((right>>8)&255);
            }
            frame+=available;
            if(available<block)break; // Preserve authored duration; remaining PCM tail stays silent.
        }
        playback.Stop();
        return new AudioStreamWav{ResourceName="Tutorial PCM from 1-2.mp3",Format=AudioStreamWav.FormatEnum.Format16Bits,LoopMode=AudioStreamWav.LoopModeEnum.Disabled,Stereo=true,MixRate=rate,Data=pcm};
    }
}
