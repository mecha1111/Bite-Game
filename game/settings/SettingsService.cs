using System;
using System.Linq;
using Godot;
using Gamejam2.Calibration;
using Gamejam2.Save;
namespace Gamejam2.Settings;
/// <summary>작은 영구 설정. 기존 audio bus/InputMap/보정 세션에만 연결한다.</summary>
public static class SettingsService
{
    public const string RhythmInputAction = "rhythm_input";
    public static readonly Vector2I[] SupportedResolutions = { new(1280, 720), new(1600, 900), new(1920, 1080) };
    public static bool Fullscreen { get; private set; }
    public static Vector2I Resolution { get; private set; } = SupportedResolutions[0];
    public static double MasterVolume { get; private set; } = .5;
    public static double MusicVolume { get; private set; } = .5;
    public static double EffectsVolume { get; private set; } = 1;
    public static bool CalibrationCompleted { get; private set; }
    public static Key RhythmKey { get; private set; }
    public static double InputOffsetSeconds
    {
        get => CalibrationSession.UserInputOffsetSeconds;
        set { CalibrationSession.UserInputOffsetSeconds = FiniteClamp(value, -.5, .5); Persist(); }
    }
    public static double VisualOffsetSeconds
    {
        get => CalibrationSession.VisualOffsetSeconds;
        set { CalibrationSession.VisualOffsetSeconds = FiniteClamp(value, -.2, .2); Persist(); }
    }
    public static void Initialize()
    {
        Fullscreen = SaveStore.Read("settings", "fullscreen", false).AsBool();
        Vector2I stored = SaveStore.Read("settings", "resolution", Resolution).AsVector2I();
        Resolution = SupportedResolutions.Contains(stored) ? stored : SupportedResolutions[0];
        MasterVolume = FiniteClamp(SaveStore.Read("settings", "master_volume", .5).AsDouble(), 0, 1);
        MusicVolume = FiniteClamp(SaveStore.Read("settings", "music_volume", .5).AsDouble(), 0, 1);
        EffectsVolume = FiniteClamp(SaveStore.Read("settings", "sfx_volume", 1.0).AsDouble(), 0, 1);
        CalibrationSession.UserInputOffsetSeconds = FiniteClamp(SaveStore.Read("settings", "input_offset", 0.0).AsDouble(), -.5, .5);
        CalibrationSession.VisualOffsetSeconds = FiniteClamp(SaveStore.Read("settings", "visual_offset", 0.0).AsDouble(), -.2, .2);
        CalibrationCompleted = SaveStore.Read("settings", "calibration_completed", false).AsBool();
        CalibrationSession.HasChoice = CalibrationCompleted;
        var fallback = InputMap.ActionGetEvents(RhythmInputAction).OfType<InputEventKey>().FirstOrDefault();
        Key key = (Key)SaveStore.Read("settings", "rhythm_key", (long)(fallback?.PhysicalKeycode ?? Key.None)).AsInt64();
        if (key != Key.None) ApplyRhythmKey(key);
        ApplyAudio(); ApplyDisplay();
    }
    private static double FiniteClamp(double value, double min, double max) => double.IsFinite(value) ? Math.Clamp(value, min, max) : 0;
    public static void CompleteCalibration()
    { CalibrationCompleted = true; CalibrationSession.HasChoice = true; Persist(); }
    public static void SetFullscreen(bool value) { Fullscreen = value; ApplyDisplay(); Persist(); }
    public static void SetResolution(int index)
    { if (index < 0 || index >= SupportedResolutions.Length) return; Resolution = SupportedResolutions[index]; ApplyDisplay(); Persist(); }
    private static void ApplyDisplay()
    {
        // One authored coordinate space for both editor and release; resolution only sizes the window.
        if(Engine.GetMainLoop() is SceneTree tree)
        {
            var window=tree.Root;
            window.ContentScaleSize=new Vector2I(1920,1080);
            window.ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect=Window.ContentScaleAspectEnum.Keep;
            window.ContentScaleFactor=1;
        }
        if (DisplayServer.GetName() == "headless") return;
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        // base viewport는 1920x1080 유지. 선택 해상도는 창 모드 크기이며 fullscreen은 현재 화면을 사용한다.
        if (!Fullscreen) DisplayServer.WindowSetSize(Resolution);
    }
    public static void RebindRhythmKey(Key key) { if (key == Key.None) return; ApplyRhythmKey(key); Persist(); }
    private static void ApplyRhythmKey(Key key)
    {
        foreach (var input in InputMap.ActionGetEvents(RhythmInputAction).OfType<InputEventKey>().ToArray()) InputMap.ActionEraseEvent(RhythmInputAction, input);
        InputMap.ActionAddEvent(RhythmInputAction, new InputEventKey { PhysicalKeycode = key }); RhythmKey = key;
    }
    public static void SetMasterVolume(double value) { MasterVolume = FiniteClamp(value, 0, 1); ApplyAudio(); Persist(); }
    public static void SetMusicVolume(double value) { MusicVolume = FiniteClamp(value, 0, 1); ApplyAudio(); Persist(); }
    public static void SetEffectsVolume(double value) { EffectsVolume = FiniteClamp(value, 0, 1); ApplyAudio(); Persist(); }
    public static void ApplyAudio()
    { ApplyBus("Master", MasterVolume); ApplyBus("Music", MusicVolume); ApplyBus("SFX", EffectsVolume); ApplyBus("Cue", EffectsVolume); }
    private static void ApplyBus(string name, double volume)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0) { GD.PushError($"설정: {name} 오디오 bus가 없습니다."); return; }
        AudioServer.SetBusMute(bus, volume == 0); AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb((float)Math.Max(.0001, volume)));
    }
    private static void Persist()
    {
        SaveStore.Write("settings", "fullscreen", Fullscreen); SaveStore.Write("settings", "resolution", Resolution);
        SaveStore.Write("settings", "master_volume", MasterVolume); SaveStore.Write("settings", "music_volume", MusicVolume); SaveStore.Write("settings", "sfx_volume", EffectsVolume);
        SaveStore.Write("settings", "input_offset", InputOffsetSeconds); SaveStore.Write("settings", "visual_offset", VisualOffsetSeconds);
        SaveStore.Write("settings", "calibration_completed", CalibrationCompleted); SaveStore.Write("settings", "rhythm_key", (long)RhythmKey); SaveStore.Flush();
    }
}
