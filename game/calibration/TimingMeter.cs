using System;
using System.Collections.Generic;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Calibration;
/// <summary>측정기가 전달한 signed 오차만 시각화한다. 목표/판정/보정값을 계산하지 않는다.</summary>
public partial class TimingMeter : Control
{
    [Export] public Control Rail { get; set; } = null!;
    [Export] public Control BadRegion { get; set; } = null!;
    [Export] public Control GoodRegion { get; set; } = null!;
    [Export] public Control PerfectRegion { get; set; } = null!;
    [Export] public Control MarkerLayer { get; set; } = null!;
    [Export] public PackedScene MarkerScene { get; set; } = null!;
    [Export] public Label ErrorLabel { get; set; } = null!;
    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public double RangeSeconds { get; set; } = .2;
    [Export(PropertyHint.Range, "0.5,5,0.1")] public double AfterimageSeconds { get; set; } = 3;
    [Export(PropertyHint.Range, "1,12,1")] public int HistoryCount { get; set; } = 8;
    private readonly List<(Control Node, double Time)> _markers = new();
    private bool _distribution;
    /// <summary>오차가 음수면 왼쪽, 양수면 오른쪽. 화면 끝은 rail 내부로 제한한다.</summary>
    public float PositionForError(double errorSeconds) => (float)((Math.Clamp(errorSeconds / RangeSeconds, -1, 1) + 1) * .5 * Rail.Size.X);
    public void Configure(RhythmJudgmentWindows windows)
    {
        foreach (var (region, width) in new[] { (BadRegion, windows.BadSeconds), (GoodRegion, windows.GoodSeconds), (PerfectRegion, windows.PerfectSeconds) })
        {
            region.AnchorLeft = (float)(.5 - Math.Min(width / RangeSeconds, 1) * .5);
            region.AnchorRight = (float)(.5 + Math.Min(width / RangeSeconds, 1) * .5);
        }
    }
    public void Reset()
    {
        foreach (var item in _markers) { MarkerLayer.RemoveChild(item.Node); item.Node.QueueFree(); }
        _markers.Clear(); _distribution = false; ErrorLabel.Text = "소리를 듣고 리듬을 맞춰보세요";
    }
    private Control Add(double error, double clock)
    {
        var node = MarkerScene.Instantiate<Control>(); MarkerLayer.AddChild(node);
        // Root is a zero-size origin; the bitmap sits inside the rail even at clamped edges.
        node.Position = new Vector2(Math.Clamp(PositionForError(error), 20, Rail.Size.X - 20), Rail.Size.Y * .5f);
        node.SetMeta("error_seconds", error); _markers.Add((node, clock)); return node;
    }
    public void Accept(double error, double clock)
    {
        if (_markers.Count > 0 && clock < _markers[^1].Time) Reset(); // same clock can restart after skipped beats
        _distribution = false; Add(error, clock);
        while (_markers.Count > HistoryCount) { var old = _markers[0].Node; MarkerLayer.RemoveChild(old); old.QueueFree(); _markers.RemoveAt(0); }
        ErrorLabel.Text = $"{error * 1000:+0;-0;0} ms · {(error < 0 ? "빠름" : error > 0 ? "늦음" : "정확한 타이밍")}";
        Refresh(clock);
    }
    public void Refresh(double clock)
    {
        if (_distribution) return;
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            var item = _markers[i]; double age = Math.Max(0, clock - item.Time);
            if (age >= AfterimageSeconds) { MarkerLayer.RemoveChild(item.Node); item.Node.QueueFree(); _markers.RemoveAt(i); continue; }
            float alpha = (float)(1 - age / AfterimageSeconds) * (i == _markers.Count - 1 ? 1 : .55f);
            item.Node.Modulate = new Color(1, 1, 1, alpha);
            item.Node.Scale = Vector2.One * (float)(1 + .25 * Math.Exp(-age * 25));
        }
    }
    public void ShowDistribution(IEnumerable<double> samples, double median)
    {
        Reset(); _distribution = true;
        foreach (var sample in samples) { var node = Add(sample, 0); node.Modulate = new Color(.7f, .9f, 1, .4f); node.Scale = Vector2.One * .8f; }
        var representative = Add(median, 0); representative.Name = "RepresentativeBias"; representative.Scale = Vector2.One * 1.4f;
        representative.Modulate = new Color(.55f, 1, 1, 1);
        ErrorLabel.Text = $"대표 입력 경향 · {median * 1000:+0;-0;0} ms {(median < 0 ? "빠름" : median > 0 ? "늦음" : "중앙")}";
    }
}
