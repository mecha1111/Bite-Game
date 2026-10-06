using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Save;
using Gamejam2.Audio;
namespace Gamejam2.Tutorial;
/// <summary>Story/checkpoint policy atop the existing gameplay composition and its sole Song Clock.</summary>
public partial class TutorialGameplayScreen : GameplayScreen
{
    public enum TutorialLessonState { Demonstrating, Practicing, Mastered, Transitioning }
    public TutorialLessonState LessonState { get; private set; }=TutorialLessonState.Demonstrating;
    public double MasteredAtSeconds { get; private set; }=double.NegativeInfinity;
    public double NextLessonTransitionSeconds { get; private set; }=double.PositiveInfinity;
    public IReadOnlyCollection<int> SkippedByMastery=>_skipped;
    private readonly HashSet<int> _skipped=new();
    private const double SuccessfulFeedbackSeconds=.85;
    private double _masteryPresentationElapsed;
    [Export] public TutorialProfile Profile { get; set; }=null!;
    [Export] public Label Message { get; set; }=null!;
    [Export] public Label Mastery { get; set; }=null!;
    [Export] public Label ModeLabel { get; set; }=null!;
    [Export] public Button SkipButton { get; set; }=null!;
    [Export] public Control SkipConfirmation { get; set; }=null!;
    [Export] public Button ContinueButton { get; set; }=null!;
    [Export] public Button ConfirmSkipButton { get; set; }=null!;
    [Export] public Sprite2D Fish { get; set; }=null!;
    [Export] public TutorialVision Vision { get; set; }=null!;
    [Export] public TutorialVision Attack { get; set; }=null!;
    public bool IsReplay { get; set; }
    public event Action? TutorialCompleted;
    public static bool HasCompleted=>SaveStore.Read("progress","tutorial_completed",false).AsBool();
    public int LessonIndex { get; private set; }
    public int SuccessCount { get; private set; }
    public int CheckpointLoops { get; private set; }
    public bool IsDemonstrating { get; private set; }=true;
    public bool IsComplete { get; private set; }
    public float RightEyeVisibility { get; private set; }=1;
    public float LeftEyeVisibility { get; private set; }=1;
    public float FishSilhouetteOpacity { get; private set; }=1;
    public float WaveSenseStrength { get; private set; }=.25f;
    public string LessonId=>Lesson["id"];
    private List<Dictionary<string,string>> _lessons=null!;
    private Dictionary<string,double>[] _values=null!;
    private Dictionary<string,string> Lesson=>_lessons[LessonIndex];
    private double Number(string key)=>_values[LessonIndex][key];
    private AudioStreamWav _tutorialMusic=null!;
    protected override AudioStream LoadMusic()=>_tutorialMusic;
    private readonly Dictionary<(int Lesson,int Block),PreyPhrase[]> _plans=new();
    private GameplayData[] _templates=null!;
    private double[] _boundaries=null!;
    private int _block;
    private double _segmentStart,_segmentEnd,_fadeInAt=double.NegativeInfinity;
    private float _lessonProgress;
    public double LastPracticeTransitionMilliseconds { get; private set; }
    public int MusicCheckpointRestarts { get; private set; }
    public double SegmentStart=>_segmentStart;
    public double SegmentEnd=>_segmentEnd;
    public bool SkipDialogVisible=>SkipConfirmation.Visible;
    private double _duration,_attackAt=double.NegativeInfinity;
    [Export] public AudioStreamPlayer AttackSound { get; set; }=null!;
    private bool _attackSoundPlayed,_demoPresentationPending,_demoVisualDone;
    public double LastDemonstrationVisualSeconds { get; private set; }
    public double LastDemonstrationChartSeconds { get; private set; }
    protected override bool DeferJudgmentPresentation=>IsDemonstrating;
    private int _failures;
    private double _completionElapsed;
    private bool _needDemo,_pendingRestart,_injurySeen,_missHint,_rightFailureSeen,_rightPracticeStarted,_completeEmitted;
    private readonly HashSet<int> _caught=new(),_missed=new();
    protected override float InterferenceAudioGain=>(float)Number("intensity");
    protected override bool ProtectSatiety=>true;
    public bool PracticeInputEnabled=>AllowBite;
    protected override bool AllowBite=>LessonState==TutorialLessonState.Practicing&&!IsDemonstrating&&!_demoPresentationPending&&!IsComplete&&!SkipConfirmation.Visible&&Rhythm.SongTimeSeconds>=_attackAt+Profile.AttackSeconds;
    protected override double StartSeconds=>_block==0?0:_segmentStart;
    private void ResetEntryState()
    {
        SongId="song_1";LessonIndex=SuccessCount=CheckpointLoops=MusicCheckpointRestarts=_block=_failures=0;
        IsComplete=false;IsDemonstrating=true;LessonState=TutorialLessonState.Demonstrating;
        LeftEyeVisibility=RightEyeVisibility=FishSilhouetteOpacity=1;WaveSenseStrength=.25f;
        _lessonProgress=0;_completionElapsed=0;_segmentStart=_segmentEnd=0;
        _attackAt=_fadeInAt=double.NegativeInfinity;
        _needDemo=_pendingRestart=_injurySeen=_missHint=_rightFailureSeen=_rightPracticeStarted=_completeEmitted=_rightEyeSideForced=false;
        _attackSoundPlayed=_demoPresentationPending=_demoVisualDone=false;
        _caught.Clear();_missed.Clear();_skipped.Clear();_plans.Clear();SkipConfirmation.Hide();AttackSound.Stop();
        Vision.Modulate=Attack.Modulate=Colors.White;Vision.Present(1,1);
        Vision.AttackAge=Attack.AttackAge=-1;Vision.Blackout=Attack.Blackout=0;Vision.QueueRedraw();Attack.QueueRedraw();
        Environment.Modulate=Environment.Background.Modulate=Environment.Background.SelfModulate=Colors.White;
        WorldWaterTreatment.Modulate=Colors.White;
        // Environment.Apply(song_1) reloads its authored palette and clears preserve_black/transition state.
    }
    public override void _Ready()
    {
        RestoreInheritedLayout();
        // Tutorial always starts in its existing shallow-coral world, never a selected custom backdrop.
        ResetEntryState();
        _lessons=LocalCsv.Read(Profile.LessonsPath);
        string[] keys={"start","end","left_from","left_to","right","silhouette","wave_from","wave_to","intensity"};
        _values=_lessons.Select(row=>keys.ToDictionary(key=>key,key=>LocalCsv.Number(row[key]))).ToArray();
        using(var source=SongAudio.Load(Profile.AudioPath)){_duration=source.GetLength();_tutorialMusic=TutorialAudio.Prepare(source);}
        if(_lessons.Count!=14||Profile.RequiredSuccesses<1)throw new ArgumentException("Tutorial lesson/profile configuration invalid.");
        double bar=240/Profile.Bpm, block=bar*6;
        double end=_duration; // Natural audio outro, followed only by a bar-aligned retry if required.
        var boundaries=new List<double>();for(double at=Profile.BeatOffsetSeconds;at<end-.001;at+=block)boundaries.Add(at);boundaries.Add(end);
        _boundaries=boundaries.ToArray();_templates=new GameplayData[_lessons.Count];
        for(int lesson=0;lesson<_lessons.Count;lesson++)
        {
            var patterns=_lessons[lesson]["patterns"].Split(';');
            _templates[lesson]=GameplayData.Tutorial(Profile.AudioPath,_duration,Profile.Bpm,Profile.BeatOffsetSeconds,0,12,patterns,_lessons[lesson]["side"],0);
            for(int part=0;part<_boundaries.Length-1;part++)
                _plans[(lesson,part)]=GameplayData.TutorialPlan(_templates[lesson],_boundaries[part],_boundaries[part+1],lesson==6?Profile.AttackSeconds+.15:0,patterns.Length);
        }
        // Decode/load before the music starts; phrase changes reuse these immutable plans.
        SfxCatalog.Load();WavePulse.Preload();GD.Load<PackedScene>("res://game/gameplay/presentation/JuiceBurst.tscn");
        SkipButton.Pressed+=OpenSkipConfirmation;ContinueButton.Pressed+=CancelSkip;ConfirmSkipButton.Pressed+=ConfirmSkip;
        Signals.Guidance=GuidanceColor;Signals.ShowResolvedTargets=true;
        base._Ready();
        if(Environment.Background.Texture==null)
            GD.PushError("Tutorial background load failed: res://assets/shallow_coral_reef_background.png");
        StandaloneSettings.TutorialButton.Disabled=true;
        StandaloneSettings.LeaveGameplayRequested+=()=>RequestExit();
    }
    private void RestoreInheritedLayout()
    {
        // The binary-exported inherited Tutorial can lose base Control anchors/offsets.
        // Read the existing Gameplay composition; never invent resolution-specific positions.
        const string path="res://game/gameplay/Gameplay.tscn";
        var scene=ResourceLoader.Load<PackedScene>(path)
            ??throw new InvalidOperationException("Tutorial layout source missing: "+path);
        var authored=scene.Instantiate<Control>();
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach(string nodePath in new[]{"Composition","EnvironmentBackground","WorldWaterTreatment",
                "Composition/SatietyGauge","SettingsButton","UnavailableLayer/Message","UnavailableLayer/Back"})
            {
                var from=authored.GetNode<Control>(nodePath);
                var to=GetNode<Control>(nodePath);
                to.Set("layout_mode",from.Get("layout_mode"));
                foreach(var side in new[]{Side.Left,Side.Top,Side.Right,Side.Bottom})
                    to.SetAnchorAndOffset(side,from.GetAnchor(side),from.GetOffset(side));
                to.GrowHorizontal=from.GrowHorizontal;to.GrowVertical=from.GrowVertical;
            }
        }
        finally{authored.Free();}
    }
    protected override GameplayData LoadData()
    {
        _segmentStart=_boundaries[_block];_segmentEnd=_boundaries[_block+1];
        return GameplayData.TutorialFromPlan(_templates[LessonIndex],_plans[(LessonIndex,_block)]);
    }
    private bool _rightEyeSideForced;
    protected override void PreparePreySides(GameplayData data)
    {
        bool forceRight=LessonIndex==6&&!_rightEyeSideForced;
        ApplyPreySideSequence(data,forceRight?false:null);
        if(forceRight)_rightEyeSideForced=true;
    }
    protected override void ConfigureInterference()
    {
        var types=Lesson["interference"].Split(';',StringSplitOptions.RemoveEmptyEntries);
        double climax=LessonIndex==13?Data.Phrases[Math.Min(1,Data.Phrases.Count-1)].TargetSeconds-1.1:_segmentStart+.8;
        var schedule=types.Select((type,i)=>(type,climax+i*.25)).ToArray();
        Interference.Initialize("song_1",schedule);
    }
    protected override void RunStarted()
    {
        _skipped.Clear();MasteredAtSeconds=double.NegativeInfinity;NextLessonTransitionSeconds=double.PositiveInfinity;
        Mastery.Modulate=Colors.White;
        _caught.Clear();_missed.Clear();_attackSoundPlayed=false;AttackSound.Stop();Fish.Visible=false;_pendingRestart=false;
        Message.Text=LessonIndex==6&&_rightFailureSeen?"보이지 않아도, 파동은 느낄 수 있습니다.":Lesson["message"];UpdateDots();
        if(LessonIndex==6&&!_injurySeen){_attackAt=_segmentStart;_injurySeen=true;Message.Text="";}
        else _attackAt=double.NegativeInfinity;
        if(LessonIndex==13)Attack.Blackout=1;
        if(LessonIndex==6&&!_rightPracticeStarted){IsDemonstrating=false;_rightPracticeStarted=true;} // Discovery comes before an explanation or assisted answer.
        StandaloneSettings.TutorialButton.Disabled=true;
        StandaloneSettings.LeaveButton.Visible=IsReplay;
        _demoPresentationPending=IsDemonstrating;_demoVisualDone=false;
        LessonState=IsDemonstrating?TutorialLessonState.Demonstrating:TutorialLessonState.Practicing;
        UpdateMode();
        Gauge.Visible=false; // Protected teaching does not display a misleading hunger deadline.
    }
    protected override void JudgmentObserved(RhythmJudgmentResult result)
    {
        int index=int.Parse(result.TargetEvent.CueId);
        if(result.Judgment!=RhythmJudgment.Miss)_caught.Add(index);else _missed.Add(index);
        if(IsDemonstrating)
        {
            IsDemonstrating=false;LessonState=TutorialLessonState.Transitioning;_practicePromptUntil=Rhythm.SongTimeSeconds+.8;Message.Text="이제 직접 해보세요";UpdateMode();return;
        }
        if(result.Judgment is RhythmJudgment.Good or RhythmJudgment.Perfect)
        {
            SuccessCount=Math.Min(Profile.RequiredSuccesses,SuccessCount+1);_failures=0;UpdateDots();UpdateMode();
            if(SuccessCount>=Profile.RequiredSuccesses)
            {
                LessonState=TutorialLessonState.Mastered;MasteredAtSeconds=Rhythm.SongTimeSeconds;_masteryPresentationElapsed=0;_needDemo=false;
                foreach(int id in SkipFuturePracticeEncounters())_skipped.Add(id);
                double bar=240/Profile.Bpm;
                NextLessonTransitionSeconds=MasteredAtSeconds;
                NextLessonTransitionSeconds=Math.Min(_duration,NextLessonTransitionSeconds);
                Message.Text="잘했어요!";
            }
        }
        else
        {
            if(++_failures>=Profile.FailuresBeforeDemo){_needDemo=true;_failures=0;}
            if(LessonIndex==6&&!_rightFailureSeen&&result.Judgment==RhythmJudgment.Miss){_missHint=true;_rightFailureSeen=true;}
        }
    }
    private void UpdateDots()=>Mastery.Text=string.Join(" ",Enumerable.Range(0,Profile.RequiredSuccesses).Select(i=>i<SuccessCount?"●":"○"));
    public override void _Process(double delta)
    {
        base._Process(delta);
        if(Data==null||RunState==GameplayRunState.Unavailable)return;
        double time=Rhythm.SongTimeSeconds;
        if(IsComplete)
        {
            if(!Rhythm.IsFocusPaused&&!InputBlocked)_completionElapsed+=delta;
            Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=Mathf.LinearToDb((float)Math.Clamp(1-_completionElapsed/.25,.001,1));
            if(!_completeEmitted&&_completionElapsed>=1.5){_completeEmitted=true;StopGameplay();TutorialCompleted?.Invoke();}
            return;
        }
        _lessonProgress=Math.Max(_lessonProgress,(float)Math.Clamp((time-_segmentStart)/(_segmentEnd-_segmentStart),0,1));
        float progress=_lessonProgress;
        LeftEyeVisibility=Mathf.Lerp((float)Number("left_from"),(float)Number("left_to"),progress);
        RightEyeVisibility=LessonIndex==6&&time-_attackAt<1.05?1:(float)Number("right");FishSilhouetteOpacity=(float)Number("silhouette");
        WaveSenseStrength=Mathf.Lerp((float)Number("wave_from"),(float)Number("wave_to"),progress);
        Vision.Present(LeftEyeVisibility,RightEyeVisibility);
        float guidanceClarity=IsDemonstrating?.9f:SuccessCount==0?.85f:SuccessCount==1?.7f:SuccessCount==2?.55f:0;
        Signals.LeftLane.Modulate=Signals.RightLane.Modulate=new Color(1,1,1,Math.Max(WaveSenseStrength,guidanceClarity));
        Interference.Effects.Modulate=Colors.White;
        PresentAttack(time);
        PresentFish(time-SettingsService.VisualOffsetSeconds);
        if(LessonState==TutorialLessonState.Mastered)
        {
            if(!InputBlocked&&!Rhythm.IsFocusPaused)_masteryPresentationElapsed+=delta;
            if(Rhythm.PlaybackState=="Completed")
            {
                // Only finish the short presentation at natural EOF; the Song Clock stays frozen.
                double effectTime=Math.Max(time,MasteredAtSeconds+_masteryPresentationElapsed);
                Shark.Present(effectTime);Feedback.Present(effectTime);Combo.Present(effectTime);Juice.Present(effectTime,effectTime,false,false,delta);
            }
            Mastery.Modulate=Colors.White.Lerp(new Color(.45f,1,.65f),(float)Math.Clamp(1-(time-MasteredAtSeconds)/SuccessfulFeedbackSeconds,0,1));
            if(!InputBlocked&&!Rhythm.IsFocusPaused&&time>=NextLessonTransitionSeconds&&!_pendingRestart)AdvanceMasteredLesson();
            return;
        }
        Message.Visible=!string.IsNullOrWhiteSpace(Message.Text);Mastery.Visible=time-_attackAt>=Profile.AttackSeconds;ModeLabel.Visible=Mastery.Visible;
        if(!Rhythm.IsClockAdvancing||InputBlocked||_pendingRestart)return;
        if(time<_fadeInAt+.18)Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=Mathf.LinearToDb((float)Math.Clamp((time-_fadeInAt)/.18,.001,1));
        else if(double.IsFinite(_fadeInAt)){Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=0;_fadeInAt=double.NegativeInfinity;}
        var first=Data.Phrases[0];
        if(IsDemonstrating&&time>=first.TargetSeconds)
        {
            Rhythm.DemonstratePrey(first.Index.ToString());
        }
        if(_demoPresentationPending&&!_demoVisualDone&&time-SettingsService.VisualOffsetSeconds>=first.TargetSeconds)
        {
            _demoVisualDone=true;LastDemonstrationVisualSeconds=time;LastDemonstrationChartSeconds=first.TargetSeconds;
            Shark.Bite(time);Juice.Bite(time);PresentBiteResult(RhythmJudgment.Perfect,time);
            if(!IsDemonstrating){_practicePromptUntil=Math.Max(_practicePromptUntil,time+.8);Message.Text="이제 직접 해보세요";UpdateMode();}
        }
        if(_demoPresentationPending&&_demoVisualDone&&!IsDemonstrating&&time>=_practicePromptUntil){_demoPresentationPending=false;LessonState=TutorialLessonState.Practicing;Message.Text="";UpdateMode();}
        bool wrap=_block==_boundaries.Length-2;
        if(wrap&&time>=_segmentEnd-.18)Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=Mathf.LinearToDb((float)Math.Clamp((_segmentEnd-time)/.18,.001,1));
        if(time>=_segmentEnd-(wrap?0:.12))AdvancePracticeBlock(wrap);
    }
    private void AdvancePracticeBlock(bool wrap)
    {
        if(_pendingRestart||IsComplete)return;
        if(SuccessCount>=Profile.RequiredSuccesses)
        {
            if(LessonIndex==_lessons.Count-1){Music.VolumeDb=0;Complete();return;}
            LessonIndex++;SuccessCount=0;_lessonProgress=0;IsDemonstrating=LessonIndex!=6&&LessonIndex!=13;_needDemo=false;_failures=0;
        }
        else{CheckpointLoops++;IsDemonstrating=_needDemo;_needDemo=false;}
        _pendingRestart=true;
        Callable.From(()=>
        {
            if(!IsInsideTree()||RunState==GameplayRunState.Abandoned)return;
            if(wrap)
            {
                _block=Math.Min(_boundaries.Length-2,(int)Math.Floor(Number("start")/(1440/Profile.Bpm)));
                ulong began=Time.GetTicksUsec();MusicCheckpointRestarts++;Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=-60;Restart();LastPracticeTransitionMilliseconds=(Time.GetTicksUsec()-began)/1000d;_fadeInAt=_segmentStart;Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=-60;
            }
            else
            {
                ulong began=Time.GetTicksUsec();_block++;InstallPracticeData(LoadData());RunStarted();Music.VolumeDb=0;LastPracticeTransitionMilliseconds=(Time.GetTicksUsec()-began)/1000d;
            }
        }).CallDeferred();
    }
    private void AdvanceMasteredLesson()
    {
        if(_pendingRestart||IsComplete)return;
        if(LessonIndex==_lessons.Count-1){Complete();return;}
        if(Rhythm.SongTimeSeconds>=_duration){AdvancePracticeBlock(true);return;} // Existing natural-EOF retry policy, never a mastery seek.
        _pendingRestart=true;LessonState=TutorialLessonState.Transitioning;
        Callable.From(()=>
        {
            if(!IsInsideTree()||RunState==GameplayRunState.Abandoned)return;
            double start=NextLessonTransitionSeconds;
            int nextLesson=LessonIndex+1;
            int block=Math.Clamp(Array.FindLastIndex(_boundaries,b=>b<=start),0,_boundaries.Length-2);
            double lead=nextLesson==6?Profile.AttackSeconds+.15:0;
            // Keep a complete next phrase even when the next bar is near a former checkpoint edge.
            while(block<_boundaries.Length-2&&_boundaries[block+1]-start<lead+300/Profile.Bpm+.4)block++;
            if(_boundaries[block+1]-start<lead+300/Profile.Bpm+.4){_pendingRestart=false;NextLessonTransitionSeconds=_duration;LessonState=TutorialLessonState.Mastered;return;}
            LessonIndex=nextLesson;SuccessCount=0;_lessonProgress=0;IsDemonstrating=LessonIndex!=6&&LessonIndex!=13;_needDemo=false;_failures=0;
            _block=block;_segmentStart=start;_segmentEnd=_boundaries[block+1];
            var plan=GameplayData.TutorialPlan(_templates[LessonIndex],start,_segmentEnd,lead,Lesson["patterns"].Split(';').Length);
            ulong began=Time.GetTicksUsec();InstallPracticeData(GameplayData.TutorialFromPlan(_templates[LessonIndex],plan));RunStarted();LastPracticeTransitionMilliseconds=(Time.GetTicksUsec()-began)/1000d;
        }).CallDeferred();
    }
    private Color GuidanceColor(int prey,int slot)
    {
        if(SuccessCount>=Profile.RequiredSuccesses)return Colors.White;
        var green=new Color(.3f,1,.45f);
        if(slot==8)return Colors.White.Lerp(green,IsDemonstrating?1:SuccessCount==0?1:SuccessCount==1?.9f:.4f);
        if(IsDemonstrating)return new Color(.45f,1,.85f); // Same real cue language with a restrained demo accent.
        return SuccessCount==0?Colors.White.Lerp(green,.65f):Colors.White;
    }
    private double _practicePromptUntil=double.NegativeInfinity;
    private void UpdateMode(){ModeLabel.Text=IsDemonstrating?"시연 중":_demoPresentationPending?"이제 직접 해보세요":SuccessCount>=3?"잘했어요!":"직접 해보기";ModeLabel.Modulate=IsDemonstrating?new Color(.55f,1,.85f):new Color(.5f,1,.66f);}
    public void OpenSkipConfirmation(){if(IsComplete||SkipConfirmation.Visible)return;SuspendForSettings();SkipConfirmation.Show();ContinueButton.GrabFocus();}
    public void CancelSkip(){if(!SkipConfirmation.Visible)return;SkipConfirmation.Hide();BeginResumeCountdown();}
    public void ConfirmSkip(){if(!SkipConfirmation.Visible)return;SaveStore.Write("progress","tutorial_completed",true);SaveStore.Flush();SkipConfirmation.Hide();StopGameplay();TutorialCompleted?.Invoke();}
    public override void _Input(InputEvent input)
    {
        // Modal UI owns Escape even while gameplay is suspended for that modal.
        if(SkipConfirmation.Visible){if(input.IsActionPressed("ui_cancel")){CancelSkip();GetViewport().SetInputAsHandled();}return;}
        // Settings suspends gameplay, but its GUI must still receive mouse/keyboard input.
        if(InputBlocked)return;
        if(input is InputEventMouseButton&&SettingsButton.GetGlobalRect().HasPoint(GetViewport().GetMousePosition()))return;
        // Ignore player Bite during demonstrations without consuming GUI actions (Space/click).
        if((LessonState==TutorialLessonState.Demonstrating||IsDemonstrating||_demoPresentationPending)&&input.IsActionPressed("rhythm_input"))
        return;
        if(SkipButton.HasFocus()&&input.IsActionPressed("ui_accept"))return;
        if(input is InputEventMouseButton&&SkipButton.GetGlobalRect().HasPoint(GetGlobalMousePosition()))return;
        base._Input(input);
    }
    private void PresentFish(double time)
    {
        Fish.Visible=false;
        if(LessonIndex>=7)return;
        foreach(var p in Data.Phrases)
        {
            if(_skipped.Contains(p.Index)||time<p.StartSeconds||time>p.TargetSeconds+1||(_caught.Contains(p.Index)&&!(p.Index==0&&_demoPresentationPending&&!_demoVisualDone))||(_demoVisualDone&&p.Index==0))continue;
            var lane=p.FromLeft?Signals.LeftLane:Signals.RightLane;
            Vector2 mouth=lane.Slots[8].OriginAnchor!.GlobalPosition;
            var bounds=GetGlobalRect();
            // Actual authored cue origins and timestamps, ending at the actual BiteTargetAnchor.
            int next=Array.FindIndex(p.Cues,cue=>cue>time);
            Vector2 from,to;double fromTime,toTime;
            if(time>=p.TargetSeconds){from=mouth;to=new Vector2(p.FromLeft?bounds.End.X+90:bounds.Position.X-90,mouth.Y-160);Fish.GlobalPosition=from.Lerp(to,(float)Math.Clamp(time-p.TargetSeconds,0,1));}
            else
            {
                if(next<0){int last=p.Cues.Length-1;from=lane.Slots[p.CueSlots[last]].GlobalPosition;fromTime=p.Cues[last];to=mouth;toTime=p.TargetSeconds;}
                else{int previous=Math.Max(0,next-1);from=lane.Slots[p.CueSlots[previous]].GlobalPosition;fromTime=p.Cues[previous];to=lane.Slots[p.CueSlots[next]].GlobalPosition;toTime=p.Cues[next];}
                Fish.GlobalPosition=from.Lerp(to,(float)Math.Clamp((time-fromTime)/Math.Max(.001,toTime-fromTime),0,1));
            }
            // The active chart segment (including escape) owns facing, not the spawn side.
            float direction=to.X-Fish.GlobalPosition.X;
            // Supplied catch_burst_frame_0 artwork naturally faces LEFT.
            if(Mathf.Abs(direction)>.01f)Fish.FlipH=direction>0;
            bool left=Fish.GlobalPosition.X<bounds.GetCenter().X;
            float sight=left?LeftEyeVisibility:RightEyeVisibility;
            Fish.Modulate=new Color(1,1,1,sight*FishSilhouetteOpacity);Fish.Visible=sight>0&&FishSilhouetteOpacity>0;
            if(_missHint&&_missed.Contains(p.Index)&&left&&time>p.TargetSeconds+.3){Message.Text="보이지 않아도, 파동은 느낄 수 있습니다.";_missHint=false;}
            break;
        }
    }
    private void PresentAttack(double time)
    {
        double age=time-_attackAt;
        Attack.AttackAge=age>=0&&age<Profile.AttackSeconds?(float)age:-1;
        if(age>=.6&&age<Profile.AttackSeconds&&!_attackSoundPlayed){_attackSoundPlayed=true;AttackSound.Play();}
        AttackSound.StreamPaused=InputBlocked||Rhythm.IsFocusPaused||Rhythm.IsManuallyPaused;
        float black=age>=.75&&age<1.05?1:age>=1.05&&age<Profile.AttackSeconds?(float)(1-(age-1.05)/(Profile.AttackSeconds-1.05)):0;
        Attack.Blackout=black;Attack.QueueRedraw();
        float shake=age>.35&&age<1.05?(float)(Math.Sin(age*95)*12*(1-(age-.35)/.7)):0;
        GetNode<Control>("Composition").Position=new Vector2(Mathf.Round(shake),Mathf.Round(-shake*.5f));
        if(shake!=0)((ShaderMaterial)WorldWaterTreatment.Material).SetShaderParameter("presentation_impulse_pixels",new Vector2(Mathf.Round(shake),Mathf.Round(shake*.5f)));
    }
    protected override void Finish()
    {
        if(IsComplete||_pendingRestart)return;
        if(LessonState==TutorialLessonState.Mastered)return; // _Process lets the final catch finish before any transition.
        AdvancePracticeBlock(true);
    }
    private void Complete()
    {
        IsComplete=true;Message.Text="이제, 파동이 보인다.";Message.Show();Fish.Visible=false;
        SaveStore.Write("progress","tutorial_completed",true);SaveStore.Flush();
    }
    public void RequestExit(){if(IsReplay){StopGameplay();TutorialCompleted?.Invoke();}}
    public bool DebugJump(string checkpoint)
    {
        if(!Gamejam2.Startup.BuildFeatures.DevelopmentEnabled||!OS.GetCmdlineUserArgs().Contains("--tutorial-debug"))return false;
        int index=_lessons.FindIndex(l=>l["id"]==checkpoint);if(index<0)return false;
        LessonIndex=index;_rightEyeSideForced=false;SuccessCount=0;IsDemonstrating=index!=6&&index!=13;_injurySeen=false;_rightFailureSeen=false;_rightPracticeStarted=false;_missHint=false;_needDemo=false;_failures=0;IsComplete=false;_block=Math.Min(_boundaries.Length-2,(int)Math.Floor(Number("start")/(1440/Profile.Bpm)));_lessonProgress=0;_fadeInAt=double.NegativeInfinity;Music.VolumeDb=AudioCues.CuePlayer.VolumeDb=0;_completeEmitted=false;_completionElapsed=0;Restart();return true;
    }
}
