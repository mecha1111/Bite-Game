using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
namespace Gamejam2.CustomGenerator;
public sealed record Attack(double Time,double Strength,double Bass,double Percussion)
{
    public double Mid {get;init;}
    public string Evidence {get;init;}="";
}
public sealed record MusicSection(double Start,double End,double Energy,string Kind);
public sealed record EnergyPoint(double Time,double Value);
public sealed record StructureChange(double Time,string Kind,double Strength);
public sealed record TempoChoice(double Bpm,double Score,double Alignment,double LeadFit);
public sealed record Analysis(double Duration,double Bpm,double PulseBpm,double Confidence,Attack[] Attacks,MusicSection[] Sections,double[] Beats,double[] Downbeats,double[] BpmCandidates,EnergyPoint[] EnergyCurve,StructureChange[] Changes)
{
    public double BeatPhase {get;init;}
    public TempoChoice[] PulseChoices {get;init;}=Array.Empty<TempoChoice>();
}
/// <summary>Local band onsets and measured energy structure. Beat grids describe evidence; they never create targets.</summary>
public static class MusicAnalysis
{
    const int Window=1024,Hop=256,Rate=11025;
    static void Fft(Complex[] x)
    {
        for(int i=1,j=0;i<x.Length;i++){int bit=x.Length>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j)(x[i],x[j])=(x[j],x[i]);}
        for(int length=2;length<=x.Length;length<<=1){var root=Complex.FromPolarCoordinates(1,-2*Math.PI/length);for(int a=0;a<x.Length;a+=length){var w=Complex.One;for(int k=0;k<length/2;k++){var u=x[a+k];var v=x[a+k+length/2]*w;x[a+k]=u+v;x[a+k+length/2]=u-v;w*=root;}}}
    }
    static double Mean(double[] values,int a,int length)
    {int end=Math.Min(values.Length,a+length);if(a>=end)return 0;double sum=0;for(int i=a;i<end;i++)sum+=values[i];return sum/(end-a);}
    static double Scale(double[] values)=>Math.Max(1e-9,Math.Sqrt(values.Sum(v=>v*v)/Math.Max(1,values.Length)));
    public static Analysis Analyze(float[] samples,double duration)
    {
        if(samples.Length<Window||!double.IsFinite(duration)||duration<=0)throw new ArgumentException("Audio is too short for analysis.");
        int count=1+(samples.Length-Window)/Hop;
        var flux=new double[count];var bass=new double[count];var mid=new double[count];var high=new double[count];var energy=new double[count];
        var previous=new double[Window/2];var spectrum=new Complex[Window];double step=(double)Hop/Rate;
        for(int f=0;f<count;f++)
        {
            for(int i=0;i<Window;i++){double v=samples[f*Hop+i];energy[f]+=v*v;spectrum[i]=v*(.5-.5*Math.Cos(2*Math.PI*i/(Window-1)));}
            Fft(spectrum);energy[f]=Math.Sqrt(energy[f]/Window);
            for(int k=1;k<Window/2;k++)
            {
                double magnitude=Math.Log(1+spectrum[k].Magnitude),onset=Math.Max(0,magnitude-previous[k]);flux[f]+=onset;
                double hz=(double)k*Rate/Window;
                if(hz<220)bass[f]+=onset;else if(hz<1800)mid[f]+=onset;else high[f]+=onset;
                previous[k]=magnitude;
            }
        }
        double fs=Scale(flux),bs=Scale(bass),ms=Scale(mid),hs=Scale(high),overall=Scale(energy);
        if(overall<1e-6)throw new InvalidOperationException("음악에서 명확한 리듬을 찾지 못했습니다.");
        var novelty=new double[count];for(int i=0;i<count;i++)novelty[i]=Math.Max(0,flux[i]/fs-Mean(flux,Math.Max(0,i-20),41)/fs);
        var attacks=new List<Attack>();
        for(int i=2;i<count-2;i++)
        {
            double local=Mean(flux,Math.Max(0,i-20),41);
            if(flux[i]<local*1.15||flux[i]<fs*.35||flux[i]<flux[i-1]||flux[i]<=flux[i+1])continue;
            // Find the beginning of this measured onset, rather than stamping a generated metronome beat.
            int onset=i;while(onset>Math.Max(0,i-3)&&flux[onset-1]>flux[i]*.45)onset--;
            double time=onset*step+Window*.5/Rate;
            if(time>=duration-.05||energy[i]<overall*.025)continue;
            var attack=new Attack(time,flux[i]/fs,bass[i]/bs,high[i]/hs){Mid=mid[i]/ms,Evidence=bass[i]/bs>high[i]/hs*1.2?"low_band_attack":high[i]/hs>mid[i]/ms*1.3?"percussion_attack":"chord_or_melody_attack"};
            if(attacks.Count>0&&time-attacks[^1].Time<.09){if(attack.Strength>attacks[^1].Strength)attacks[^1]=attack;continue;}
            attacks.Add(attack);
        }
        if(attacks.Count==0)throw new InvalidOperationException("음악에서 명확한 리듬을 찾지 못했습니다.");
        // Normalized, locally detrended correlation avoids a sustained loud bed flattening BPM confidence.
        var tempos=new List<(double Bpm,double Score)>();
        for(int bpm=65;bpm<=180;bpm++)
        {
            int lag=(int)Math.Round(60.0/bpm/step);double cross=0,a=0,b=0;
            for(int i=lag;i<count;i++){cross+=novelty[i]*novelty[i-lag];a+=novelty[i]*novelty[i];b+=novelty[i-lag]*novelty[i-lag];}
            tempos.Add((bpm,cross/Math.Max(1e-9,Math.Sqrt(a*b))));
        }
        var best=tempos.OrderByDescending(t=>t.Score).First();double confidence=Math.Clamp(best.Score,0,1),musical=best.Bpm,beat=60/musical;
        double phase=0,bestPhase=-1;
        for(int p=0;p<48;p++)
        {
            double candidate=p*beat/48,score=0;
            foreach(var attack in attacks){double cycles=(attack.Time-candidate)/beat;double distance=Math.Abs(cycles-Math.Round(cycles));score+=Math.Min(4,attack.Strength)*Math.Exp(-distance*distance/.012);}
            if(score>bestPhase){bestPhase=score;phase=candidate;}
        }
        var curve=Enumerable.Range(0,(count+42)/43).Select(i=>new EnergyPoint(i*43*step,Mean(energy,i*43,43))).ToArray();
        var boundaries=new List<double>{0};double reference=Mean(energy,0,Math.Min(count,172));
        for(int i=172;i<count-86;i+=43)
        {
            double e=Mean(energy,i,86),time=i*step;
            if(time-boundaries[^1]>=4&&(e>Math.Max(overall*.03,reference)*1.5||e<reference*.62))
            {boundaries.Add(time);reference=e;}
        }
        boundaries.Add(duration);var sections=new List<MusicSection>();
        for(int i=0;i<boundaries.Count-1;i++)
        {
            int a=(int)(boundaries[i]/step),length=Math.Max(1,(int)((boundaries[i+1]-boundaries[i])/step));
            double e=Mean(energy,a,length);sections.Add(new(boundaries[i],boundaries[i+1],e,e<overall*.22?"break":e<overall*.60?"low_activity":"active"));
        }
        var changes=new List<StructureChange>();
        for(int i=1;i<sections.Count;i++)
        {
            var s=sections[i];var before=sections[i-1];
            changes.Add(new(s.Start,s.Kind=="break"?"release":s.Energy>before.Energy*1.4?"drop":"section",s.Energy/Math.Max(overall*.05,before.Energy)));
        }
        for(int i=3;i<curve.Length;i++)
        {
            double before=curve[i-3].Value,now=curve[i].Value;
            if(now>Math.Max(overall*.2,before)*1.45&&curve[i-1].Value>=curve[i-2].Value&&curve[i-2].Value>=before)
                changes.Add(new(curve[i].Time,"buildup",now/Math.Max(overall*.05,before)));
        }
        var choices=new List<TempoChoice>();
        foreach(double pulse in new[]{musical/2,musical,musical*2,musical*4}.Where(v=>v>=50&&v<=360).Distinct())
        {
            double tick=60/pulse,alignment=0,weight=0,fit=0;
            foreach(var attack in attacks)
            {
                double cycles=(attack.Time-phase)/tick,dist=Math.Abs(cycles-Math.Round(cycles)),w=Math.Min(4,attack.Strength);
                alignment+=w*Math.Exp(-dist*dist/.018);weight+=w;
            }
            // Compare how many genuine salient attacks can support a readable four-pulse lead-in.
            double previousTarget=-10;foreach(var a in attacks.Where(a=>a.Strength>=1.1))if(a.Time-previousTarget>=4*tick+.3){fit++;previousTarget=a.Time;}
            double active=sections.Where(s=>s.Kind=="active").Sum(s=>s.End-s.Start);
            double density=fit/Math.Max(1,active),natural=1-Math.Min(1,Math.Abs(4*tick-1.5)/2);
            double score=.55*alignment/Math.Max(1e-9,weight)+.25*natural+.20*Math.Min(1,density/.45);
            choices.Add(new(pulse,score,alignment/Math.Max(1e-9,weight),fit));
        }
        double chosen=choices.OrderByDescending(c=>c.Score).First().Bpm;
        var beats=Enumerable.Range(0,(int)(duration/beat)+1).Select(i=>phase+i*beat).Where(t=>t<duration).ToArray();
        // Collapse close structural reports; no fixed-time schedule or global-confidence suppression.
        var selectedChanges=new List<StructureChange>();
        foreach(var change in changes.OrderByDescending(c=>c.Strength))if(selectedChanges.All(c=>Math.Abs(c.Time-change.Time)>2))selectedChanges.Add(change);
        return new(duration,musical,chosen,confidence,attacks.ToArray(),sections.ToArray(),beats,beats.Where((_,i)=>i%4==0).ToArray(),choices.Select(c=>c.Bpm).ToArray(),curve,selectedChanges.OrderBy(c=>c.Time).ToArray()){BeatPhase=phase,PulseChoices=choices.ToArray()};
    }
}
