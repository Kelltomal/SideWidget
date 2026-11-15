using System.Buffers.Binary;
using SideWidget.Core;

static class AudioSpectrumChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
        void Reject(Action action, string name)
        {
            try { action(); } catch (Exception ex) when (ex is NotSupportedException or ArgumentException) { count++; return; }
            throw new Exception(name);
        }
        var format = new AudioPcmFormat(48000, 2, 32, 8, true);
        byte[] Tone(double frequency)
        {
            var bytes = new byte[4096 * 8];
            for (var i = 0; i < 4096; i++)
            {
                var sample = (float)(.5 * Math.Sin(2 * Math.PI * frequency * i / 48000));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8), BitConverter.SingleToInt32Bits(sample));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8 + 4), BitConverter.SingleToInt32Bits(-sample));
            }
            return bytes;
        }
        var analyzer = new AudioSpectrumAnalyzer(format);
        var empty = analyzer.Snapshot(1);
        Check(empty.Available && empty.Rms == 0 && empty.Bands.Length == 32 && empty.Bands.All(x => x == 0), "new analyzer is truthful silence");
        analyzer.Append(Tone(220)); var bass = analyzer.Snapshot(2);
        analyzer.Reset(); analyzer.Append(Tone(5000)); var treble = analyzer.Snapshot(3);
        int Peak(AudioSpectrumFrame f) => Array.IndexOf(f.Bands, f.Bands.Max());
        Check(Peak(treble) > Peak(bass) + 8, "FFT peak follows source frequency");
        Check(bass.Rms is > .34 and < .37, "opposite-phase stereo does not cancel");
        Check(treble.Bands.All(x => float.IsFinite(x) && x is >= 0 and <= 1), "spectrum bands bounded and finite");
        analyzer.AppendSilence(4096);
        for (var i = 0; i < 30; i++) empty = analyzer.Snapshot(4 + i);
        Check(empty.Rms == 0 && empty.Bands.All(x => x == 0), "silence decays fully to zero");
        analyzer.Append(Tone(220)); analyzer.Reset();
        Check(analyzer.Snapshot(50).Bands.All(x => x == 0), "reset clears stale samples and envelope");
        var pcm16 = new AudioPcmFormat(44100, 1, 16, 2, false);
        Check(pcm16.Decode(new byte[] { 0, 128 }) == -1 && pcm16.Decode(new byte[] { 255, 127 }) > .999, "signed PCM16 extremes");
        var pcm24 = new AudioPcmFormat(48000, 1, 24, 3, false);
        Check(pcm24.Decode(new byte[] { 0, 0, 128 }) == -1 && pcm24.Decode(new byte[] { 255, 255, 127 }) > .999, "PCM24 sign extension");
        var pcm32 = new AudioPcmFormat(96000, 1, 32, 4, false);
        Check(pcm32.Decode(new byte[] { 0, 0, 0, 128 }) == -1, "PCM32 signed normalization");
        var nan = BitConverter.GetBytes(float.NaN);
        Check(format.Decode(nan) == 0 && format.Decode(BitConverter.GetBytes(float.PositiveInfinity)) == 0, "nonfinite float samples become silence");
        var wave = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(wave, 0xfffe);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(2), 6);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 96000);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(12), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(16), 22);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(18), 24);
        new Guid("00000001-0000-0010-8000-00aa00389b71").TryWriteBytes(wave.AsSpan(24));
        var extensible = AudioPcmFormat.Parse(wave);
        Check(extensible.Channels == 6 && !extensible.FloatingPoint && extensible.Decode(new byte[] { 0, 0, 0, 64 }) == .5f, "extensible 24 valid bits in PCM32 container");
        new Guid("00000003-0000-0010-8000-00aa00389b71").TryWriteBytes(wave.AsSpan(24));
        Check(AudioPcmFormat.Parse(wave).FloatingPoint, "extensible float subtype recognized");
        wave[24] = 42;
        Reject(() => AudioPcmFormat.Parse(wave), "unknown extensible subtype rejected");
        Reject(() => AudioPcmFormat.Parse(new byte[12]), "short format rejected");
        Reject(() => new AudioSpectrumAnalyzer(format).Append(new byte[7]), "partial PCM frame rejected");
        Reject(() => new AudioSpectrumAnalyzer(new(48000, 0, 32, 0, true)), "invalid channels rejected");
        var padded = new AudioSpectrumAnalyzer(new(48000, 1, 16, 4, false));
        padded.Append(new byte[] { 0, 64, 255, 255 });
        Check(padded.Snapshot(1).Rms > 0, "block alignment padding skipped");
        // A huge packet must produce exactly the newest window, not stale prefix data.
        var huge = new byte[4096 * 8 * 3]; Tone(220).CopyTo(huge, 0);
        analyzer.Append(huge);
        for (var i = 0; i < 30; i++) empty = analyzer.Snapshot(100 + i);
        Check(empty.Rms == 0 && empty.Bands.All(x => x == 0), "oversized packets keep bounded newest window");
        return count;
    }
}
