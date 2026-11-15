using System.Buffers.Binary;

namespace SideWidget.Core;

public sealed record AudioSpectrumFrame(bool Available, string? Message, float[] Bands, double Rms, long Sequence);

/// <summary>Uncompressed interleaved WAVEFORMATEX / WAVEFORMATEXTENSIBLE mix format.</summary>
public sealed record AudioPcmFormat(int SampleRate, int Channels, int BitsPerSample, int BlockAlign, bool FloatingPoint)
{
    public static AudioPcmFormat Parse(ReadOnlySpan<byte> wave)
    {
        if (wave.Length < 18) throw new NotSupportedException("Incomplete audio mix format.");
        var tag = BinaryPrimitives.ReadUInt16LittleEndian(wave);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(wave[2..]);
        var rate = BinaryPrimitives.ReadInt32LittleEndian(wave[4..]);
        var align = BinaryPrimitives.ReadUInt16LittleEndian(wave[12..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(wave[14..]);
        if (tag == 0xfffe)
        {
            if (wave.Length < 40 || BinaryPrimitives.ReadUInt16LittleEndian(wave[16..]) < 22)
                throw new NotSupportedException("Incomplete extensible audio format.");
            var subtype = new Guid(wave.Slice(24, 16));
            tag = subtype == new Guid("00000001-0000-0010-8000-00aa00389b71") ? (ushort)1 :
                subtype == new Guid("00000003-0000-0010-8000-00aa00389b71") ? (ushort)3 : (ushort)0;
            var valid = BinaryPrimitives.ReadUInt16LittleEndian(wave[18..]);
            if (valid > bits) throw new NotSupportedException("Invalid audio valid-bit count.");
            // Extensible integer samples are left aligned in their containers.
        }
        var result = new AudioPcmFormat(rate, channels, bits, align, tag == 3);
        result.Validate();
        if (tag is not (1 or 3)) throw new NotSupportedException("Unsupported audio encoding.");
        return result;
    }

    public void Validate()
    {
        if (SampleRate is < 8000 or > 768000 || Channels is < 1 or > 32 ||
            (FloatingPoint ? BitsPerSample != 32 : BitsPerSample is not (16 or 24 or 32)) ||
            BlockAlign < Channels * (BitsPerSample / 8) || BlockAlign > 4096)
            throw new NotSupportedException("Unsupported audio mix format.");
    }

    public float Decode(ReadOnlySpan<byte> sample)
    {
        double value = FloatingPoint ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample)) : BitsPerSample switch
        {
            16 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768d,
            24 => ((sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8) / 8388608d,
            32 => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648d,
            _ => 0
        };
        return double.IsFinite(value) ? (float)Math.Clamp(value, -1, 1) : 0;
    }
}

/// <summary>Bounded streaming FFT. Caller serializes access; snapshots own their band arrays.</summary>
public sealed class AudioSpectrumAnalyzer
{
    public const int BandCount = 32;
    private const int Size = 4096;
    private readonly AudioPcmFormat format;
    private readonly float[] samples = new float[Size];
    private readonly double[] real = new double[Size], imaginary = new double[Size];
    private readonly float[] envelope = new float[BandCount];
    private int cursor;

    public AudioSpectrumAnalyzer(AudioPcmFormat format) { format.Validate(); this.format = format; }

    public void Append(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length % format.BlockAlign != 0) throw new ArgumentException("PCM packet contains an incomplete frame.", nameof(pcm));
        // Only the newest FFT window matters, even after a delayed capture poll.
        var first = Math.Max(0, pcm.Length - Size * format.BlockAlign);
        for (var offset = first; offset < pcm.Length; offset += format.BlockAlign)
        {
            float mono = 0;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var value = format.Decode(pcm[(offset + channel * (format.BitsPerSample / 8))..]);
                // Peak channel preserves opposite-phase stereo instead of cancelling it.
                if (Math.Abs(value) > Math.Abs(mono)) mono = value;
            }
            samples[cursor] = mono;
            cursor = (cursor + 1) % Size;
        }
    }

    public void AppendSilence(int frames)
    {
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
        if (frames >= Size) { Array.Clear(samples); cursor = 0; return; }
        for (var i = 0; i < frames; i++) { samples[cursor] = 0; cursor = (cursor + 1) % Size; }
    }

    public void Reset() { Array.Clear(samples); Array.Clear(envelope); cursor = 0; }

    public AudioSpectrumFrame Snapshot(long sequence)
    {
        double energy = 0;
        for (var i = 0; i < Size; i++)
        {
            var value = samples[(cursor + i) % Size];
            energy += value * value;
            real[i] = value * (.5 - .5 * Math.Cos(2 * Math.PI * i / (Size - 1)));
            imaginary[i] = 0;
        }
        Transform();
        var bands = new float[BandCount];
        var top = Math.Min(20000d, format.SampleRate / 2d);
        for (var band = 0; band < BandCount; band++)
        {
            var low = 30 * Math.Pow(top / 30, band / (double)BandCount);
            var high = 30 * Math.Pow(top / 30, (band + 1d) / BandCount);
            var start = Math.Clamp((int)Math.Floor(low * Size / format.SampleRate), 1, Size / 2 - 1);
            var end = Math.Clamp((int)Math.Ceiling(high * Size / format.SampleRate), start + 1, Size / 2);
            double peak = 0;
            for (var bin = start; bin < end; bin++) peak = Math.Max(peak, Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]) * 4 / Size);
            var target = peak < .0001 ? 0 : (float)Math.Clamp((20 * Math.Log10(peak) + 80) / 80, 0, 1);
            var value = target >= envelope[band] ? target : envelope[band] * .65f;
            bands[band] = envelope[band] = value < .001f ? 0 : value;
        }
        return new(true, null, bands, Math.Sqrt(energy / Size), sequence);
    }

    private void Transform()
    {
        for (int i = 1, j = 0; i < Size; i++)
        {
            var bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (real[i], real[j]) = (real[j], real[i]);
        }
        for (var length = 2; length <= Size; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var stepReal = Math.Cos(angle); var stepImaginary = Math.Sin(angle);
            for (var start = 0; start < Size; start += length)
            {
                double wr = 1, wi = 0;
                for (var j = 0; j < length / 2; j++)
                {
                    var a = start + j; var b = a + length / 2;
                    var tr = wr * real[b] - wi * imaginary[b]; var ti = wr * imaginary[b] + wi * real[b];
                    real[b] = real[a] - tr; imaginary[b] = imaginary[a] - ti;
                    real[a] += tr; imaginary[a] += ti;
                    (wr, wi) = (wr * stepReal - wi * stepImaginary, wr * stepImaginary + wi * stepReal);
                }
            }
        }
    }
}
