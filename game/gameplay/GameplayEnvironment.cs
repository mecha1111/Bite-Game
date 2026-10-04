using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Gameplay;
/// <summary>Song-specific art and restrained ambience. Shader/particles freeze with the existing Song Clock.</summary>
public partial class GameplayEnvironment : Control
{
    // Cache native names: string conversions allocate disposable StringName wrappers each frame.
    private static readonly StringName UniformPreserveBlack="preserve_black";
    private static readonly StringName UniformAmbientTint = "ambient_tint";
    private static readonly StringName UniformCausticScale = "caustic_scale";
    private static readonly StringName UniformCausticSpeed = "caustic_speed";
    private static readonly StringName UniformCausticStrength = "caustic_strength";
    private static readonly StringName UniformCurrentPixels = "current_pixels";
    private static readonly StringName UniformFailureStrength = "failure_strength";
    private static readonly StringName UniformFogStrength = "fog_strength";
    private static readonly StringName UniformRayOpacity = "ray_opacity";
    private static readonly StringName UniformRefractionSpeed = "refraction_speed";
    private static readonly StringName UniformRefractionStrength = "refraction_strength";
    private static readonly StringName UniformSaturation = "saturation";
    private static readonly StringName UniformSharkTintStrength = "shark_tint_strength";
    private static readonly StringName UniformTimeSeconds = "time_seconds";
    private static readonly StringName UniformTransitionPressure = "transition_pressure";
    private static readonly StringName UniformVentStrength = "vent_strength";
    private static readonly StringName UniformVignetteStrength = "vignette_strength";
    private static readonly StringName UniformWhiteReduction = "white_reduction";
    private static readonly StringName UniformWorldTint = "world_tint";

    [Export] public TextureRect PreviousBackground { get; set; } = null!;
    public GameplayJuiceProfile? JuiceProfile { get; set; }
    [Export] public TextureRect Background { get; set; } = null!;
    [Export] public ColorRect ReadabilityShade { get; set; } = null!;
    [Export] public CpuParticles2D LeftParticles { get; set; } = null!;
    [Export] public CpuParticles2D RightParticles { get; set; } = null!;
    [Export] public CpuParticles2D FarParticles { get; set; } = null!;
    public ColorRect? WorldWaterOverlay { get; set; }
    public WorldWaterProfile? WorldProfile { get; private set; }
    public string Ambience { get; private set; } = "";
    public string ActiveEnvironmentSongId { get; private set; } = "";
    private Dictionary<string,Dictionary<string,string>> _environmentRows=new();
    private (double Time,string Profile,string Style,double Duration)[] _sections=Array.Empty<(double,string,string,double)>();
    // Immutable resources are shared across retries; retain managed C# resource wrappers as well as native cache entries.
    private static readonly Dictionary<string,Texture2D> _backgrounds=new();
    private static readonly Dictionary<string,WorldWaterProfile> _profiles=new();
    private ShaderMaterial? _previousMaterial;
    private float _ambientFxSpeed=1;
    private readonly Dictionary<StringName,Variant> _worldFrom=new(),_worldTo=new(),_artFrom=new(),_artTo=new();
    private static readonly StringName[] WorldKeys={"world_tint","shark_tint_strength","white_reduction","refraction_strength","refraction_speed","caustic_scale","caustic_speed","fog_strength","saturation","caustic_strength","ray_opacity","vignette_strength"};
    private static readonly StringName[] ArtKeys={"ambient_tint","vent_strength","vignette_strength"};
    private Color _shadeFrom,_shadeTo,_particleFrom,_particleTo;
    private int _densityFrom,_densityTo;
    private double _transitionAt=double.NegativeInfinity,_transitionDuration=.6;
    private string _transitionStyle="crossfade";
    private float _particleMotion=1;
    public float TransitionProgress { get; private set; } = 1;
    private int _nextSection;
    private bool _starved;
    public void Apply(string songId)
    {
        _worldFrom.Clear();_worldTo.Clear();_artFrom.Clear();_artTo.Clear();
        if(WorldWaterOverlay?.Material is ShaderMaterial reset){reset.SetShaderParameter(UniformTransitionPressure,0f);reset.SetShaderParameter(UniformPreserveBlack,0f);}
        _starved=false;_nextSection=0;_transitionAt=double.NegativeInfinity;TransitionProgress=1;_particleMotion=1;PreviousBackground.Visible=false;
        _sections=LocalCsv.Read("res://data/balance/environment_sequence.csv").Where(r=>r["song_id"]==songId)
            .Select(r=>(LocalCsv.Number(r["start_time_sec"]),r["environment_song_id"],r.TryGetValue("transition_style",out var style)?style:"crossfade",r.TryGetValue("transition_duration_sec",out var life)?LocalCsv.Number(life):.6)).OrderBy(s=>s.Item1).ToArray();
        var custom=CustomMapRegistry.Find(songId);
        double duration=custom?.Duration ?? LocalCsv.Number(LocalCsv.Read("res://data/balance/songs.csv").Single(r=>r["song_id"]==songId)["duration_seconds"]);
        _environmentRows=LocalCsv.Read("res://data/balance/environments.csv").ToDictionary(r=>r["song_id"]);
        if(_backgrounds.Count==0)foreach(var entry in _environmentRows)
        {
            string background=entry.Value["background_path"],profile=$"res://game/gameplay/water_profiles/{entry.Key}.tres";
            _backgrounds[entry.Key]=GD.Load<Texture2D>(background)??throw new InvalidOperationException("필수 배경 리소스 없음: "+background);
            _profiles[entry.Key]=GD.Load<WorldWaterProfile>(profile)??throw new InvalidOperationException("필수 월드 데이터 없음: "+profile);
        }
        var ids=_environmentRows.Keys.ToHashSet();
        if(_sections.Any(s=>!double.IsFinite(s.Time)||s.Time<0||s.Time>=duration||!ids.Contains(s.Profile)||!double.IsFinite(s.Duration)||s.Duration<.2||s.Duration>1.2) || _sections.GroupBy(s=>s.Time).Any(g=>g.Count()>1)
            || (_sections.Length>0&&_sections[0].Time!=0))throw new ArgumentException($"Environment sequence invalid: {songId}");
        ApplyProfile(_sections.Length>0?_sections[_nextSection++].Profile:custom?.EnvironmentProfile??songId);
        if(custom!=null&&!custom.Generated)
        {
            if(custom.BackgroundPath.Length>0)Background.Texture=GD.Load<Texture2D>(custom.BackgroundPath);
            else
            {
                Background.SelfModulate=Colors.Black;ReadabilityShade.Color=Colors.Transparent;
                if(WorldWaterOverlay?.Material is ShaderMaterial black){black.SetShaderParameter(UniformPreserveBlack,1f);black.SetShaderParameter(UniformFogStrength,0f);black.SetShaderParameter(UniformRayOpacity,0f);}
                LeftParticles.Emitting=RightParticles.Emitting=FarParticles.Emitting=false;
            }
        }
        if(_sections.Length>1)_previousMaterial??=(ShaderMaterial)Background.Material.Duplicate();
    }
    private void ApplyProfile(string songId)
    {
        ActiveEnvironmentSongId=songId;
        var row=_environmentRows[songId];
        Background.Texture=_backgrounds[songId];
        Background.SelfModulate=Colors.White;
        ReadabilityShade.Color=new Color(.005f,.03f,.08f,(float)LocalCsv.Number(row["background_dim"]));
        Ambience=row["ambience"];
        var shader=(ShaderMaterial)Background.Material;
        shader.SetShaderParameter(UniformFailureStrength,_starved ? .4f : 0f);
        shader.SetShaderParameter(UniformRayOpacity,0f);
        shader.SetShaderParameter(UniformCausticStrength,0f);
        shader.SetShaderParameter(UniformVentStrength,(float)LocalCsv.Number(row["vent_strength"]));
        shader.SetShaderParameter(UniformVignetteStrength,(float)LocalCsv.Number(row["vignette_strength"]));
        var tint=Color.FromHtml(row["ambient_tint"]);
        shader.SetShaderParameter(UniformAmbientTint,new Vector3(tint.R,tint.G,tint.B));
        WorldProfile=_profiles[songId];
        if(WorldWaterOverlay?.Material is ShaderMaterial world)
        {
            world.SetShaderParameter(UniformWorldTint,new Vector3(tint.R,tint.G,tint.B));
            world.SetShaderParameter(UniformSharkTintStrength,WorldProfile.SharkTintStrength);
            world.SetShaderParameter(UniformWhiteReduction,WorldProfile.WhiteReduction);
            world.SetShaderParameter(UniformRefractionStrength,WorldProfile.RefractionStrength);
            world.SetShaderParameter(UniformRefractionSpeed,WorldProfile.RefractionSpeed);
            world.SetShaderParameter(UniformCausticScale,WorldProfile.CausticScale);
            world.SetShaderParameter(UniformCausticSpeed,WorldProfile.CausticSpeed);
            world.SetShaderParameter(UniformFogStrength,WorldProfile.FogStrength);
            world.SetShaderParameter(UniformSaturation,WorldProfile.Saturation);
            world.SetShaderParameter(UniformCausticStrength,(float)LocalCsv.Number(row["caustic_strength"]));
            world.SetShaderParameter(UniformRayOpacity,(float)LocalCsv.Number(row["light_ray_strength"]));
            world.SetShaderParameter(UniformVignetteStrength,(float)LocalCsv.Number(row["vignette_strength"]));
            world.SetShaderParameter(UniformFailureStrength,_starved ? .45f : 0f);
        }
        tint.A=(float)LocalCsv.Number(row["particle_opacity"]);
        LeftParticles.Modulate=RightParticles.Modulate=tint;
        FarParticles.Modulate=new Color(tint.R,tint.G,tint.B,tint.A*.55f);
        int density=(int)LocalCsv.Number(row["particle_density"]);
        LeftParticles.Amount=RightParticles.Amount=density;
        FarParticles.Amount=System.Math.Max(2,density);
    }
    public void SetStarved()
    {
        _starved=true;
        ((ShaderMaterial)Background.Material).SetShaderParameter(UniformFailureStrength,.4f);
        if(WorldWaterOverlay?.Material is ShaderMaterial world)world.SetShaderParameter(UniformFailureStrength,.45f);
    }
    private void BeginTransition(string songId,double time,string style,double duration)
    {
        var world=WorldWaterOverlay?.Material as ShaderMaterial;
        var art=(ShaderMaterial)Background.Material;
        foreach(var key in WorldKeys)if(world!=null)_worldFrom[key]=world.GetShaderParameter(key);
        foreach(var key in ArtKeys)_artFrom[key]=art.GetShaderParameter(key);
        _shadeFrom=ReadabilityShade.Color;_particleFrom=LeftParticles.Modulate;_densityFrom=LeftParticles.Amount;
        _previousMaterial??=(ShaderMaterial)art.Duplicate();
        foreach(var key in ArtKeys)_previousMaterial.SetShaderParameter(key,art.GetShaderParameter(key));
        _previousMaterial.SetShaderParameter(UniformFailureStrength,art.GetShaderParameter(UniformFailureStrength));
        PreviousBackground.Texture=Background.Texture;PreviousBackground.Material=_previousMaterial;PreviousBackground.SelfModulate=Colors.White;PreviousBackground.Visible=true;
        ApplyProfile(songId);
        foreach(var key in WorldKeys)if(world!=null)_worldTo[key]=world.GetShaderParameter(key);
        foreach(var key in ArtKeys)_artTo[key]=art.GetShaderParameter(key);
        _shadeTo=ReadabilityShade.Color;_particleTo=LeftParticles.Modulate;_densityTo=LeftParticles.Amount;
        _transitionAt=time;_transitionStyle=style;
        _transitionDuration=duration;
        BlendTransition(time);
    }
    private static Variant Blend(Variant from,Variant to,float weight)=>from.VariantType==Variant.Type.Vector3
        ? Variant.From(from.AsVector3().Lerp(to.AsVector3(),weight))
        : Variant.From(Mathf.Lerp(from.AsSingle(),to.AsSingle(),weight));
    private void BlendTransition(double time)
    {
        TransitionProgress=(float)Math.Clamp((time-_transitionAt)/_transitionDuration,0,1);
        float phase=TransitionProgress,ease=phase*phase*(3-2*phase);
        if(_worldTo.Count>0&&WorldWaterOverlay?.Material is ShaderMaterial world)
        {foreach(var key in WorldKeys)world.SetShaderParameter(key,Blend(_worldFrom[key],_worldTo[key],ease));
         world.SetShaderParameter(UniformTransitionPressure,_transitionStyle=="pressure"?Mathf.Sin(phase*Mathf.Pi)*.00015f:0);}
        var art=(ShaderMaterial)Background.Material;
        foreach(var key in ArtKeys)if(_artTo.ContainsKey(key))art.SetShaderParameter(key,Blend(_artFrom[key],_artTo[key],ease));
        ReadabilityShade.Color=_shadeFrom.Lerp(_shadeTo,ease);
        LeftParticles.Modulate=RightParticles.Modulate=_particleFrom.Lerp(_particleTo,ease);
        var tint=LeftParticles.Modulate;FarParticles.Modulate=new Color(tint.R,tint.G,tint.B,tint.A*.55f);
        int density=(int)Math.Round(Mathf.Lerp(_densityFrom,_densityTo,ease));
        if(LeftParticles.Amount!=density)LeftParticles.Amount=RightParticles.Amount=density;
        if(FarParticles.Amount!=Math.Max(2,density))FarParticles.Amount=Math.Max(2,density);
        Background.SelfModulate=new Color(1,1,1,ease);
        PreviousBackground.Visible=phase<1;
    }
    public void Present(double songTime,bool paused,double delta=0)
    {
        while(_nextSection<_sections.Length&&_sections[_nextSection].Time<=songTime)
        {var section=_sections[_nextSection++];BeginTransition(section.Profile,section.Time,section.Style,section.Duration);}
        if(PreviousBackground.Visible)BlendTransition(songTime);
        ((ShaderMaterial)Background.Material).SetShaderParameter(UniformTimeSeconds,songTime);
        if(PreviousBackground.Material is ShaderMaterial previous)previous.SetShaderParameter(UniformTimeSeconds,songTime);
        if(WorldWaterOverlay?.Material is ShaderMaterial world)world.SetShaderParameter(UniformTimeSeconds,songTime);
        // Only ambience eases visually. Audio, target events, rings and drain are already frozen.
        _particleMotion=Mathf.MoveToward(_particleMotion,paused?0:1,(float)(delta/Math.Max(.01,JuiceProfile?.PauseEaseSeconds??.25)));
        LeftParticles.SpeedScale=RightParticles.SpeedScale=FarParticles.SpeedScale=_particleMotion*_ambientFxSpeed;
    }
    public void ApplyPresentationMotion(float currentPixels,float energy)
    {
        _ambientFxSpeed=1+Math.Clamp(energy,0,.2f);
        FarParticles.Direction=new Vector2(-.3f-currentPixels*.04f,-1);
        ((ShaderMaterial)Background.Material).SetShaderParameter(UniformCurrentPixels,currentPixels);
        _previousMaterial?.SetShaderParameter(UniformCurrentPixels,currentPixels);
    }
}
