// Deterministic offline audio evidence. This is deliberately a small signal analyser rather than
// an aesthetic classifier: it exposes stationary tones, silence, seams and spectral shape so a
// human can make an informed soundscape decision.
using System;
using UnityEngine;

[Serializable]
public sealed class GmAudioClipMetrics
{
    public float rms;
    public float peak;
    public float silenceShare;
    public float spectralCentroidHz;
    public float spectralFlatness;
    public float persistentToneDb;
    public float stationarity;
    public float loopDiscontinuity;
    public float loopSimilarity;
    public float boundaryJump;
    public float boundarySlopeJump;
    public float sampleStepRms;
    public float boundaryJumpRatio;
    public bool spaceshipRisk;
}

public static class GmAudioAnalysis
{
    const int WindowSize = 512;
    const int WindowCount = 20;

    public static GmAudioClipMetrics Measure(AudioClip clip)
    {
        var metrics = new GmAudioClipMetrics();
        if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
            return metrics;

        int frames = Mathf.Min(clip.samples, clip.frequency * 120);
        float[] interleaved = new float[frames * clip.channels];
        if (!clip.LoadAudioData() || !clip.GetData(interleaved, 0)) return metrics;
        var mono = new float[frames];
        double sumSquares = 0d;
        float peak = 0f;
        for (int frame = 0; frame < frames; frame++)
        {
            float sample = 0f;
            for (int channel = 0; channel < clip.channels; channel++)
                sample += interleaved[frame * clip.channels + channel];
            sample /= clip.channels;
            mono[frame] = sample;
            sumSquares += sample * sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }
        metrics.rms = (float)Math.Sqrt(sumSquares / Math.Max(1, frames));
        metrics.peak = peak;

        const int envelopeBlock = 1024;
        int blocks = Mathf.Max(1, frames / envelopeBlock);
        var blockRms = new float[blocks];
        int silent = 0;
        double envelopeMean = 0d;
        for (int block = 0; block < blocks; block++)
        {
            int start = block * envelopeBlock;
            int end = Mathf.Min(frames, start + envelopeBlock);
            double energy = 0d;
            for (int i = start; i < end; i++) energy += mono[i] * mono[i];
            float value = (float)Math.Sqrt(energy / Math.Max(1, end - start));
            blockRms[block] = value;
            envelopeMean += value;
            if (value < 0.0025f) silent++;
        }
        metrics.silenceShare = silent / (float)blocks;
        envelopeMean /= blocks;
        double envelopeVariance = 0d;
        foreach (float value in blockRms)
            envelopeVariance += (value - envelopeMean) * (value - envelopeMean);
        envelopeVariance /= blocks;
        float coefficient = envelopeMean <= 0.000001d ? 0f :
            (float)(Math.Sqrt(envelopeVariance) / envelopeMean);
        metrics.stationarity = 1f / (1f + coefficient);

        MeasureSpectrum(mono, clip.frequency, metrics);
        MeasureLoop(mono, metrics);
        // Two different failures read as "spaceship" in a quiet exterior: an obvious narrow
        // drone, or a merely tonal bed whose envelope never breathes and whose seam keeps
        // announcing the loop. The second case is what the original Wend Hill wind slipped
        // through when this only looked for a very strong spectral spike.
        bool narrowDrone = metrics.persistentToneDb >= 8f &&
            metrics.stationarity >= 0.58f && metrics.silenceShare < 0.08f;
        bool mechanicalBed = metrics.persistentToneDb >= 5f &&
            metrics.stationarity >= 0.72f && metrics.silenceShare < 0.03f &&
            metrics.loopDiscontinuity > 0.12f;
        metrics.spaceshipRisk = narrowDrone || mechanicalBed;
        return metrics;
    }

    static void MeasureSpectrum(float[] mono, int sampleRate, GmAudioClipMetrics metrics)
    {
        if (mono.Length < WindowSize) return;
        const int bins = WindowSize / 4;
        var totalPower = new double[bins];
        var dominantBins = new int[WindowCount];
        double flatnessSum = 0d;
        int measured = 0;
        for (int window = 0; window < WindowCount; window++)
        {
            int start = Mathf.RoundToInt((mono.Length - WindowSize) *
                (window / (float)Mathf.Max(1, WindowCount - 1)));
            var power = new double[bins];
            double arithmetic = 0d;
            double logSum = 0d;
            int dominant = 1;
            for (int bin = 1; bin < bins; bin++)
            {
                double real = 0d, imaginary = 0d;
                double frequency = 2d * Math.PI * bin / WindowSize;
                for (int n = 0; n < WindowSize; n++)
                {
                    double hann = 0.5d - 0.5d * Math.Cos(2d * Math.PI * n / (WindowSize - 1));
                    double sample = mono[start + n] * hann;
                    real += sample * Math.Cos(frequency * n);
                    imaginary -= sample * Math.Sin(frequency * n);
                }
                double value = real * real + imaginary * imaginary + 1e-12d;
                power[bin] = value;
                totalPower[bin] += value;
                arithmetic += value;
                logSum += Math.Log(value);
                if (value > power[dominant]) dominant = bin;
            }
            dominantBins[window] = dominant;
            arithmetic /= Math.Max(1, bins - 1);
            flatnessSum += Math.Exp(logSum / Math.Max(1, bins - 1)) / Math.Max(1e-12d, arithmetic);
            measured++;
        }

        double weighted = 0d, total = 0d;
        int globalDominant = 1;
        for (int bin = 1; bin < bins; bin++)
        {
            double hz = bin * sampleRate / (double)WindowSize;
            weighted += totalPower[bin] * hz;
            total += totalPower[bin];
            if (totalPower[bin] > totalPower[globalDominant]) globalDominant = bin;
        }
        metrics.spectralCentroidHz = total <= 0d ? 0f : (float)(weighted / total);
        metrics.spectralFlatness = measured == 0 ? 0f : (float)(flatnessSum / measured);

        double neighbours = 0d;
        int neighbourCount = 0;
        for (int offset = -3; offset <= 3; offset++)
        {
            if (Math.Abs(offset) <= 1) continue;
            int bin = globalDominant + offset;
            if (bin <= 0 || bin >= bins) continue;
            neighbours += totalPower[bin];
            neighbourCount++;
        }
        double neighbourMean = neighbours / Math.Max(1, neighbourCount);
        double ratio = totalPower[globalDominant] / Math.Max(1e-12d, neighbourMean);
        int persistent = 0;
        foreach (int bin in dominantBins)
            if (Math.Abs(bin - globalDominant) <= 1) persistent++;
        float persistence = persistent / (float)Math.Max(1, dominantBins.Length);
        metrics.persistentToneDb = Mathf.Max(0f, (float)(10d * Math.Log10(Math.Max(1d, ratio))) * persistence);
    }

    static void MeasureLoop(float[] mono, GmAudioClipMetrics metrics)
    {
        if (mono.Length >= 3)
        {
            metrics.boundaryJump = Mathf.Abs(mono[0] - mono[mono.Length - 1]);
            float firstSlope = mono[1] - mono[0];
            float wrapSlope = mono[0] - mono[mono.Length - 1];
            metrics.boundarySlopeJump = Mathf.Abs(firstSlope - wrapSlope);
            double stepSquares = 0d;
            for (int i = 1; i < mono.Length; i++)
            {
                float step = mono[i] - mono[i - 1];
                stepSquares += step * step;
            }
            metrics.sampleStepRms = (float)Math.Sqrt(stepSquares / (mono.Length - 1));
            metrics.boundaryJumpRatio = metrics.boundaryJump /
                Mathf.Max(0.000001f, metrics.sampleStepRms);
        }
        int count = Mathf.Min(2048, mono.Length / 2);
        if (count <= 0) return;
        double difference = 0d, firstEnergy = 0d, lastEnergy = 0d, dot = 0d;
        int lastStart = mono.Length - count;
        for (int i = 0; i < count; i++)
        {
            float first = mono[i];
            float last = mono[lastStart + i];
            float delta = first - last;
            difference += delta * delta;
            firstEnergy += first * first;
            lastEnergy += last * last;
            dot += first * last;
        }
        metrics.loopDiscontinuity = (float)Math.Sqrt(difference / count);
        metrics.loopSimilarity = firstEnergy <= 1e-12d || lastEnergy <= 1e-12d ? 0f :
            Mathf.Clamp((float)(dot / Math.Sqrt(firstEnergy * lastEnergy)), -1f, 1f);
    }
}
