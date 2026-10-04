using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
namespace Gamejam2.CustomGenerator;
public sealed record Attack(double Time,double Strength,double Bass,double Percussion);
public sealed record MusicSection(double Start,double End,double Energy,string Kind);
public sealed record EnergyPoint(double Time,double Value);
public sealed record StructureChange(double Time,string Kind,double Strength);
public sealed record Analysis(double Duration,double Bpm,double PulseBpm,double Confidence,Attack[] Attacks,MusicSection[] Sections,double[] Beats,double[] Downbeats,double[] BpmCandidates,EnergyPoint[] EnergyCurve,StructureChange[] Changes);
/// <summary>CPU-only local spectral flux, band attacks, tempo hypotheses and energy segmentation.</summary>
public static class MusicAnalysis
{
    const int Window=1024,Hop=256,Rate=11025;
    static void Fft(Complex[] x)
    {
        for(int i=1,j=0;i<x.Length;i++){int bit=x.Length>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j)(x[i],x[j])=(x[j],x[i]);}
        for(int length=2;length<=x.Length;length<<=1){var root=Complex.FromPolarCoordinates(1,-2*Math.PI/length);for(int a=0;a<x.Length;a+=length){var w=Complex.One;for(int k=0;k<length/2;k++){var u=x[a+k];var v=x[a+k+length/2]*w;x[a+k]=u+v;x[a+k+length/2]=u-v;w*=root;}}}
    }
    public static Analysis Analyze(float[] samples,double duration)
    {
        int count=Math.Max(1,(samples.Length-Window)/Hop);var flux=new double[count];var bass=new double[count];var high=new double[count];var energy=new double[count];var previous=new double[Window/2];var spectrum=new Complex[Window];
        for(int f=0;f<count;f++)
        {
            for(int i=0;i<Window;i++){double v=samples[Math.Min(samples.Length-1,f*Hop+i)];energy[f]+=v*v;spectrum[i]=v*(.5-.5*Math.Cos(2*Math.PI*i/(Window-1)));}
            Fft(spectrum);energy[f]=Math.Sqrt(energy[f]/Window);
            for(int k=1;k<Window/2;k++){double magnitude=Math.Log(1+spectrum[k].Magnitude),attack=Math.Max(0,magnitude-previous[k]);flux[f]+=attack;if(k*Rate/Window<220)bass[f]+=attack;if(k*Rate/Window>1800)high[f]+=attack;previous[k]=magnitude;}
        }
        var attacks=new List<Attack>();double step=(double)Hop/Rate;
        for(int i=2;i<count-2;i++)
        {
            double local=flux.Skip(Math.Max(0,i-20)).Take(41).Average();
            if(flux[i]>local*1.35&&flux[i]>=flux[i-1]&&flux[i]>flux[i+1]&&flux[i]>1)
                attacks.Add(new(i*step+Window*.5/Rate,flux[i],bass[i],high[i]));
        }
        var tempos=new List<(double Bpm,double Score)>();
        for(int bpm=65;bpm<=180;bpm++){int lag=(int)Math.Round(60/bpm/step);double score=0;for(int i=lag;i<count;i++)score+=flux[i]*flux[i-lag];tempos.Add((bpm,score));}
        var best=tempos.OrderByDescending(t=>t.Score).First();double mean=tempos.Average(t=>t.Score),confidence=Math.Clamp((best.Score/Math.Max(1e-9,mean)-1)*2,0,1);
        double musical=best.Bpm,pulse=musical<105?musical*2:musical;
        // Faster lead-ins for energetic music, without changing the musical attack timestamps.
        if(attacks.Count/Math.Max(1,duration)>2.5&&pulse<220)pulse*=2;
        var sections=new List<MusicSection>();double overall=energy.Average();double start=0,lastEnergy=energy.Take(Math.Min(count,172)).Average();
        for(int i=172;i<count;i+=43)
        {
            double e=energy.Skip(i).Take(172).DefaultIfEmpty(0).Average();
            if(i*step-start>4&&(e>lastEnergy*1.65||e<lastEnergy*.55))
            {sections.Add(new(start,i*step,lastEnergy,lastEnergy<overall*.3?"break":lastEnergy<overall*.65?"low_activity":"active"));start=i*step;lastEnergy=e;}
        }
        sections.Add(new(start,duration,lastEnergy,lastEnergy<overall*.3?"break":lastEnergy<overall*.65?"low_activity":"active"));
        double beat=60/musical,origin=attacks.OrderByDescending(a=>a.Strength+a.Bass*2).Take(20).OrderBy(a=>a.Time).FirstOrDefault()?.Time??0;
        origin%=beat;var beats=Enumerable.Range(0,(int)(duration/beat)+1).Select(i=>origin+i*beat).Where(t=>t<duration).ToArray();
        var curve=Enumerable.Range(0,(count+42)/43).Select(i=>new EnergyPoint(i*43*step,energy.Skip(i*43).Take(43).Average())).ToArray();
        var changes=new List<StructureChange>();
        for(int i=3;i<curve.Length;i++)
        {
            double before=curve[i-3].Value,now=curve[i].Value;
            if(now>before*1.5&&curve[i-2].Value>=before&&curve[i-1].Value>=curve[i-2].Value)changes.Add(new(curve[i].Time,"buildup",now/Math.Max(.0001,before)));
            if(now>curve[i-1].Value*1.8){var hit=attacks.Where(a=>Math.Abs(a.Time-curve[i].Time)<1.2).OrderByDescending(a=>a.Strength+a.Bass*2).FirstOrDefault();if(hit!=null)changes.Add(new(hit.Time,"drop",hit.Strength));}
        }
        foreach(var section in sections.Skip(1))changes.Add(new(section.Start,"section",section.Energy));
        return new(duration,musical,pulse,confidence,attacks.ToArray(),sections.ToArray(),beats,beats.Where((_,i)=>i%4==0).ToArray(),new[]{musical/2,musical,musical*2},curve,changes.OrderBy(c=>c.Time).ToArray());
    }
}
