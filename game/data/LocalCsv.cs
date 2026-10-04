using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
namespace Gamejam2.Data;
/// <summary>Packaged UTF-8 CSV reader. No network requests or silent balance fallbacks.</summary>
public static class LocalCsv
{
    public static List<Dictionary<string,string>> Read(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"필수 CSV 읽기 실패: {path} (Godot: {FileAccess.GetOpenError()})");
        var header = file.GetCsvLine();
        if(header.Length==0||string.IsNullOrWhiteSpace(header[0]))throw new InvalidOperationException("CSV 헤더 없음: "+path);
        header[0]=header[0].TrimStart('\uFEFF'); var rows = new List<Dictionary<string,string>>();
        while (!file.EofReached())
        {
            var values = file.GetCsvLine(); if (values.Length == 1 && values[0] == "") continue;
            if (values.Length != header.Length) throw new InvalidOperationException($"CSV 열 수 오류: {path}");
            var row = new Dictionary<string,string>();
            for(int i=0;i<header.Length;i++) row.Add(header[i], values[i].StartsWith("res://",StringComparison.Ordinal)?values[i].Normalize(NormalizationForm.FormC):values[i]); rows.Add(row);
        }
        Gamejam2.Startup.ReleaseDiagnostics.Loaded("CSV",path);
        return rows;
    }
    public static double Number(string value)
    {
        double number = double.Parse(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number)) throw new ArgumentException("CSV 값은 유한수여야 합니다."); return number;
    }
}
