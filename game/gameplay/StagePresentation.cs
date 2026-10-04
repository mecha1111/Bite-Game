using System;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Read-only Song Clock observer. Camera2D operates on WORLD canvas only, never gameplay node transforms.</summary>
public partial class StagePresentation : Node
{
    // Cache native names: string conversions allocate disposable StringName wrappers each frame.
    private static readonly StringName UniformStageCameraOffset = "stage_camera_offset";
    private static readonly StringName UniformStageCameraZoom = "stage_camera_zoom";
    private static readonly StringName UniformStageCameraPivot = "stage_camera_pivot";
    private static readonly StringName UniformStageCaustic = "stage_caustic";
    private static readonly StringName UniformStageColor = "stage_color";
    private static readonly StringName UniformStageDim = "stage_dim";
    private static readonly StringName UniformStageLight = "stage_light";
    private static readonly StringName UniformStageVignette = "stage_vignette";
    private static readonly string[] InterferenceTypes={"ship_horn","whale","fishing_float","sardine"};

    [Export] public SharkJumpBubbleEffect AccentParticles { get; set; }=null!;
    [Export(PropertyHint.Range,"0,1,.05")] public float Intensity { get; set; }=1;
    public WorldCameraRig? WorldCamera {get;private set;}
    public void InstallWorldCamera()
    {
        if(!_enabled||WorldCamera!=null)return;
        WorldCamera=new WorldCameraRig{Name="WorldCamera"};AddChild(WorldCamera);WorldCamera.Install(_screen);
        Present(_screen.Rhythm.SongTimeSeconds);
    }
    public Vector2 CameraOffset { get; private set; }
    public float CameraZoom { get; private set; }=1;
    public float ReadabilityMultiplier { get; private set; }=1;
    public int EventsTriggered { get; private set; }
    public PresentationEnergy CurrentEnergy {get;private set;}=PresentationEnergy.MEDIUM;
    public StageFxEvent[] Events { get; private set; }=Array.Empty<StageFxEvent>();
    private GameplayScreen _screen=null!;
    private ShaderMaterial _water=null!;
    private bool _enabled;
    private int _next;
    private GameplayData _data=null!;
    private bool _subscribed;
    private double _interferenceAt=double.NegativeInfinity;
    private string _interferenceType="";
    private bool _main;
    private bool _csvOnlyCamera;
    private PresentationSection[] _sections=Array.Empty<PresentationSection>();
    private Vector2 _interferenceOrigin;
    private double _lastResponseAt=double.NegativeInfinity;
    private int _floatEntrances;
    public override void _Ready()=>SetProcess(false);
    public void Initialize(GameplayScreen screen,GameplayData data,bool enabled)
    {
        _screen=screen;_data=data;_enabled=enabled;_csvOnlyCamera=data.ChartPath.EndsWith("_v5.csv",StringComparison.Ordinal);_water=(ShaderMaterial)screen.WorldWaterTreatment.Material;
        _main=MainStagePresentationProfile.IsMain(screen.SongId);_lastResponseAt=double.NegativeInfinity;_floatEntrances=0;
        _sections=Array.Empty<PresentationSection>();Events=Array.Empty<StageFxEvent>();
        try
        {
            if(enabled)
            {
                _sections=MainStagePresentationProfile.LoadSections(screen.SongId);
                Events=StagePresentationChart.Load(screen.SongId,data.DurationSeconds);
                if(_main){_=MainStagePresentationProfile.Policy("normal_offset");foreach(string type in InterferenceTypes)_=MainStagePresentationProfile.Interference(type);}
            }
        }
        catch(Exception error)
        {
            GD.PushWarning($"Optional presentation unavailable [{screen.SongId}]; Gameplay continues: {error.Message}");
            _enabled=false;_main=false;Events=Array.Empty<StageFxEvent>();_sections=Array.Empty<PresentationSection>();
        }
        _next=EventsTriggered=0;_interferenceAt=double.NegativeInfinity;
        if(!_subscribed){screen.Interference.Started+=ObserveInterference;screen.Interference.Emitted+=ObserveEmission;_subscribed=true;}
        AccentParticles.Reset();Present(0);
    }
    private void ObserveInterference(string type,double time)
    {
        if(!_enabled||_csvOnlyCamera)return;
        if(type=="fishing_float"&&++_floatEntrances%2==0)return;
        // A shot is a whole interference entrance, never a response to every emission.
        if(time-_lastResponseAt<12)return;
        foreach(var e in Events)if(IsCamera(e.Type)&&time>=e.Time-5&&time<e.Time+e.Duration+5)return;
        _interferenceOrigin=_screen.Interference.GetEffect(type).GlobalPosition;
        _lastResponseAt=time;_interferenceType=type;_interferenceAt=time;
    }
    private void ObserveEmission(string type,Vector2 origin,double time) { } // Waves repeat inside one held composition.
    private PresentationEnergy EnergyAt(double time)
    {foreach(var s in _sections)if(time>=s.Start&&time<s.End)return s.Level;return _sections.Length>0?PresentationEnergy.LOW:PresentationEnergy.MEDIUM;}
    private static bool IsCamera(string type)=>type is not ("COLOR_PULSE" or "LIGHT_PULSE" or "color_pulse" or "light_boost" or "caustic_boost" or "particle_burst" or "vignette_pulse" or "world_flash");
    private Vector2 WaveOrigin(StageFxEvent e)
    {
        if(e.TargetWorldPosition is { } authored)return authored;
        var lane=e.Target=="left"?_screen.Signals.LeftLane:_screen.Signals.RightLane;
        PreyPhrase? closest=null;double distance=double.PositiveInfinity;
        foreach(var p in _data.Phrases)if(p.FromLeft==(e.Target=="left")&&Math.Abs(p.TargetSeconds-e.Time)<distance){closest=p;distance=Math.Abs(p.TargetSeconds-e.Time);}
        // A source shot must use an incoming signal origin, never the mouth/target slot.
        return lane.Slots[closest?.CueSlots.Length>0?closest.CueSlots[0]:0].GlobalPosition;
    }
    private static float EaseShot(double value)=>.5f-.5f*Mathf.Cos((float)Math.Clamp(value,0,1)*Mathf.Pi);
    private static float ShotEnvelope(double age,double entry,double hold,double exit)
    {
        if(age<0||age>=entry+hold+exit)return 0;
        return age<entry?EaseShot(age/entry):age<entry+hold?1:1-EaseShot((age-entry-hold)/exit);
    }
    public void ObserveJudgment(RhythmJudgment grade,double time) { } // Ordinary catches never steer the camera.
    private static float Pulse(double age,double life)=>age<0||age>=life?0:age<.035?(float)(age/.035):(float)Math.Pow(1-(age-.035)/Math.Max(.001,life-.035),2);
    private static Vector3 Tint(string id)=>id switch{"vent"=>new(.7f,.27f,.11f),"teal"=>new(.14f,.65f,.55f),"cyan"=>new(.24f,.7f,.86f),_=>new(.08f,.22f,.42f)};
    public void Present(double time)
    {
        if(_water==null)return;
        CurrentEnergy=EnergyAt(time);
        ReadabilityMultiplier=1;
        if(_enabled)
        {
            foreach(var p in _data.Phrases)
                if(time>=p.StartSeconds&&time<=p.TargetSeconds+.3&&p.PatternId is "ex1" or "m3" or "s2"){ReadabilityMultiplier=.8f;break;}
            if(_screen.Interference.GetEffect("sardine").IsActive||_screen.Interference.GetEffect("whale").IsActive)ReadabilityMultiplier=Math.Min(ReadabilityMultiplier,.7f);
            int competing=0;foreach(string type in InterferenceTypes)if(_screen.Interference.GetEffect(type).IsActive)competing++;
            if(competing>=2)ReadabilityMultiplier=Math.Min(ReadabilityMultiplier,.55f);
        }
        float amount=_enabled?Intensity*ReadabilityMultiplier:0;
        while(_next<Events.Length&&Events[_next].Time<=time)
        {
            var e=Events[_next++];EventsTriggered++;
            if(e.Type=="particle_burst"&&time-e.Time<.15&&amount>0)
            {
                var rect=_screen.GetGlobalRect();var position=rect.Position+rect.Size*new Vector2(e.Target=="left"?.22f:e.Target=="right"?.78f:.5f,.91f);
                var tint=Tint(e.Parameter);AccentParticles.Emit(e.Time,position,(int)Math.Clamp(e.Strength*amount*(_main?MainStagePresentationProfile.Gain(CurrentEnergy):1),4,24),new Color(tint.X,tint.Y,tint.Z),_next);
            }
        }
        Vector2 pan=Vector2.Zero;float zoom=0,dim=0,vignette=0,caustic=0,light=0;Vector3 color=Vector3.Zero;bool strong=false,cinematic=false;
        for(int i=_next-1;i>=0;i--)
        {
            var e=Events[i];double age=time-e.Time;if(age>8)break;if(age<0||age>=e.Duration)continue;
            if(_main&&!e.AuthoredV5&&EnergyAt(e.Time)==PresentationEnergy.LOW&&IsCamera(e.Type)&&e.Level!=PresentationEnergy.CLIMAX)continue;
            float pulse=Pulse(age,e.Duration),value=(float)e.Strength*pulse;
            float progress=(float)(age/e.Duration);
            float smooth=progress*progress*(3-2*progress);
            float arc=Mathf.Sin(smooth*Mathf.Pi);
            float shot=ShotEnvelope(age,e.EntrySeconds,e.HoldSeconds,e.ExitSeconds);
            float strength=(float)e.Strength;
            float zoomDelta=(float)e.ZoomDelta;
            if(_main){float gain=e.Level==PresentationEnergy.CLIMAX?1:MainStagePresentationProfile.Gain(EnergyAt(e.Time));value*=gain;strength*=gain;strong|=e.Level==PresentationEnergy.CLIMAX;}
            if(e.AuthoredV5)
            {
                float Ease(double x)
                {
                    float t=(float)Math.Clamp(x,0,1);
                    return e.Ease=="CUBIC_IN_OUT"?(t<.5f?4*t*t*t:1-Mathf.Pow(-2*t+2,3)/2):.5f-.5f*Mathf.Cos(t*Mathf.Pi);
                }
                float weight=age<e.EntrySeconds?Ease(age/e.EntrySeconds):age<e.EntrySeconds+e.HoldSeconds?1:1-Ease((age-e.EntrySeconds-e.HoldSeconds)/e.ExitSeconds);
                cinematic=true;
                pan.X+=(float)e.PanPercent*_screen.GetGlobalRect().Size.X*weight;
                zoom+=(age<e.EntrySeconds?(float)e.ZoomFrom-1+((float)e.ZoomTo-(float)e.ZoomFrom)*weight:((float)e.ZoomTo-1)*weight);
                continue;
            }
            switch(e.Type)
            {
                case "KICK_PULSE":zoom+=zoomDelta*pulse;break;
                case "BASS_PUSH":pan+=e.PositionOffset*pulse;zoom+=zoomDelta*pulse;break;
                case "BUILDUP_ZOOM":cinematic=true;zoom+=zoomDelta*shot;break;
                case "LIGHT_PULSE":light+=value*arc;caustic+=value*.5f*arc;break;
                case "INTERFERENCE_FOCUS":cinematic=true;pan+=e.PositionOffset*shot;zoom+=zoomDelta*shot;break;
                case "FOCUS_WAVE":case "FOLLOW_WAVE":
                    Vector2 origin=WaveOrigin(e);
                    Vector2 center=_screen.Juice.MouthAnchor.GlobalPosition;
                    cinematic=true;
                    Vector2 focus=(origin-center)*Math.Clamp((float)e.Strength*.20f,.15f,.25f);
                    focus.Y=0; // Source framing is horizontal; keep the mouth inside the play field.
                    pan+=focus*shot;zoom+=Math.Clamp(zoomDelta,.08f,.12f)*shot;
                    if(e.Type=="FOLLOW_WAVE")zoom=(float)(.10-.18*EaseShot((age-e.EntrySeconds-.5)/1.2))*shot;
                    break;
                case "ZOOM_TO_SHARK":cinematic=true;zoom+=Math.Clamp(zoomDelta,.12f,.15f)*shot;break;
                case "IMPACT_ZOOM":zoom+=zoomDelta*pulse;pan+=e.PositionOffset*pulse;break;
                case "PULL_BACK":cinematic=true;zoom-=Math.Max(.08f,Math.Abs(zoomDelta))*shot;break;
                case "SIDE_PAN":cinematic=true;pan+=new Vector2((e.Target=="left"?-1:1)*_screen.GetGlobalRect().Size.X*.12f,e.PositionOffset.Y)*shot;zoom+=zoomDelta*shot;break;
                case "PRESSURE_PUSH":cinematic=true;pan+=e.PositionOffset*shot;zoom+=zoomDelta*shot;break;
                case "DROP_ZOOM":case "SECTION_TRANSITION":
                    cinematic=true;zoom=-.09f*shot;
                    pan+=e.PositionOffset*shot;caustic+=.008f*arc;light+=.01f*arc;break;
                case "COLOR_PULSE":color+=Tint(e.Parameter)*value;break;
                case "camera_pulse":case "camera_zoom":cinematic=true;zoom+=(float)e.Strength*shot;break;
                case "camera_kick":pan+=new Vector2(e.Target=="left"?-value:value,-value*.35f);break;
                case "camera_pan":case "camera_follow_wave":pan+=new Vector2((e.Target=="left"?1:-1)*(float)e.Strength*shot,0);break;
                case "camera_drop":cinematic=true;zoom-=.08f*shot;break;
                case "world_flash":dim+=value;break;
                case "color_pulse":color+=Tint(e.Parameter)*value;break;
                case "vignette_pulse":vignette+=value;break;
                case "caustic_boost":caustic+=value;break;
                case "light_boost":light+=value;break;
            }
        }
        double interferenceAge=time-_interferenceAt;
        var profile=_interferenceType.Length>0?MainStagePresentationProfile.Interference(_interferenceType):null;
        if(profile!=null)
        {
            float response=ShotEnvelope(interferenceAge,1,Math.Max(.5,profile.Duration-2.5),1.5);
            if(response>0)
            {
                cinematic=true;
                Vector2 direction=(_interferenceOrigin-_screen.Juice.MouthAnchor.GlobalPosition).Normalized();
                if(_interferenceType=="sardine")direction=Vector2.Up;
                pan+=direction*profile.Offset*response;
                zoom+=profile.Zoom*response;
            }
        }
        float cap=cinematic?_screen.GetGlobalRect().Size.X*.35f:_main?(float)MainStagePresentationProfile.Policy(strong?"climax_offset":"normal_offset"):strong?22:18;
        float cameraAmount=cinematic?Intensity:amount;
        CameraOffset=(_main?pan.LimitLength(cap):new Vector2(Math.Clamp(pan.X,-cap,cap),Math.Clamp(pan.Y,-cap,cap)))*cameraAmount;
        CameraZoom=1+Math.Clamp(zoom,cinematic?-.12f:-.03f,cinematic?.22f:strong?.075f:.07f)*cameraAmount;
        if(_main)
        {
            CameraZoom=cinematic?Math.Clamp(CameraZoom,.88f,1.22f):Math.Clamp(CameraZoom,(float)MainStagePresentationProfile.Policy("normal_zoom_min"),(float)MainStagePresentationProfile.Policy("normal_zoom_max"));
            float lightBudget=Math.Abs(zoom)>.02||pan.Length()>9?(float)MainStagePresentationProfile.Policy("light_budget_when_camera_strong"):1;
            color*=lightBudget;light*=lightBudget;caustic*=lightBudget;dim*=lightBudget;
        }
        var viewport=GetViewport().GetVisibleRect().Size;
        var mouth=_screen.Juice.MouthAnchor.GlobalPosition;
        // Upward misdirection must not push the last readable mouth pixels below the screen.
        CameraOffset=new(CameraOffset.X,Math.Max(CameraOffset.Y,Math.Min(0,(mouth.Y-viewport.Y*.97f)/CameraZoom)));
        _water.SetShaderParameter(UniformStageCameraPivot,mouth/viewport);
        if(WorldCamera!=null)
        {
            WorldCamera.ApplyShot(CameraOffset,CameraZoom,mouth);
            // Real camera already transformed WORLD; never apply a second screen-space zoom.
            _water.SetShaderParameter(UniformStageCameraOffset,Vector2.Zero);_water.SetShaderParameter(UniformStageCameraZoom,1f);
        }
        else {_water.SetShaderParameter(UniformStageCameraOffset,CameraOffset);_water.SetShaderParameter(UniformStageCameraZoom,CameraZoom);}
        _water.SetShaderParameter(UniformStageColor,color*amount);_water.SetShaderParameter(UniformStageDim,Math.Min(.08f,dim)*amount);
        _water.SetShaderParameter(UniformStageVignette,Math.Min(.08f,vignette)*amount);_water.SetShaderParameter(UniformStageCaustic,Math.Min(.02f,caustic)*amount);_water.SetShaderParameter(UniformStageLight,Math.Min(.025f,light)*amount);
        float drift=_main?Mathf.Sin((float)time*.16f)*(CurrentEnergy==PresentationEnergy.LOW?.35f:.8f):0;
        _screen.Environment.ApplyPresentationMotion(_screen.SongId=="song_2"?CameraOffset.X+drift:drift*.25f,light*15*amount);
        AccentParticles.Present(time);
    }
}
