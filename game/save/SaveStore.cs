using Godot;
namespace Gamejam2.Save;
/// <summary>하나의 ConfigFile IO만 담당한다. settings/progress는 별도 service가 소유한다.</summary>
public static class SaveStore
{
    public const string DefaultPath = "user://player_settings.cfg";
    private static ConfigFile _file = new();
    private static string _path = DefaultPath;
    public static void Initialize(string path = DefaultPath)
    {
        _path = path; _file = new ConfigFile();
        var error = _file.Load(path);
        if (error != Error.Ok && error != Error.FileNotFound) GD.PushWarning($"설정 읽기 실패: {error}. 기본값 사용.");
    }
    public static Variant Read(string section, string key, Variant fallback) => _file.GetValue(section, key, fallback);
    public static void Write(string section, string key, Variant value) => _file.SetValue(section, key, value);
    public static void EraseSection(string section){if(_file.HasSection(section))_file.EraseSection(section);}
    public static void Flush()
    { var error = _file.Save(_path); if (error != Error.Ok) GD.PushError($"설정 저장 실패: {error}"); }
}
