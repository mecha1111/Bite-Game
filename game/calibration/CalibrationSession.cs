namespace Gamejam2.Calibration;
/// <summary>보정 런타임 값만 보관한다. SettingsService가 영구 설정을 주입/저장하며 별도 시계는 없다.</summary>
public static class CalibrationSession
{
    public static bool HasChoice { get; set; }
    /// <summary>판정 시계에 더한다. raw 입력이 늦으면 음수 보정을 제안한다.</summary>
    public static double UserInputOffsetSeconds { get; set; }
    /// <summary>양수는 화면만 지연시킨다. audio/target에는 사용하지 않는다.</summary>
    public static double VisualOffsetSeconds { get; set; }
    public static void ResetOffsets() { UserInputOffsetSeconds = 0; VisualOffsetSeconds = 0; }
}
