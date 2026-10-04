using System;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.Audio;
/// <summary>검증과 보정에 필요한 click PCM만 합성한다. clock/재생/판정은 소유하지 않는다.</summary>
internal static class MetronomeTrack
{
    private const int SampleRate = 22050;
    public static AudioStreamWav CreateTrack(IReadOnlyList<double> beatTimes, double durationSeconds)
    {
        var samples = new double[(int)Math.Ceiling(durationSeconds * SampleRate)];
        foreach (double time in beatTimes) AddTone(samples, time, 1000, 0.025, 0.22);
        return ToPcm(samples);
    }
	private static void AddTone(double[] samples, double timeSeconds, double frequency, double durationSeconds, double amplitude)
	{
		int startSample = (int)Math.Round(timeSeconds * SampleRate);
		int count = (int)(durationSeconds * SampleRate);
		for (int index = 0; index < count && startSample + index < samples.Length; index++)
		{
			double envelope = Math.Min(1, index / (SampleRate * 0.001)) * Math.Pow(1 - (double)index / count, 3);
			samples[startSample + index] += amplitude * envelope * Math.Sin(2 * Math.PI * frequency * index / SampleRate);
		}
	}
	private static AudioStreamWav ToPcm(double[] samples)
	{
		var bytes = new byte[samples.Length * 2];
		for (int index = 0; index < samples.Length; index++)
		{
			short sample = (short)(Math.Clamp(samples[index], -1, 1) * short.MaxValue);
			bytes[index * 2] = (byte)(sample & 0xff);
			bytes[index * 2 + 1] = (byte)((sample >> 8) & 0xff);
		}
		return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SampleRate, Data = bytes };
	}
}
