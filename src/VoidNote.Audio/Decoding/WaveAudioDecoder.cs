using System.Buffers.Binary;
using System.Text;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Decoding;

/// <summary>Streams uncompressed PCM and IEEE-float RIFF/WAVE without an external dependency.</summary>
public sealed class WaveAudioDecoder : IAudioDecoder
{
    private const int FramesPerChunk = 4096;
    public string BackendId => "voidnote-wave-1";
    public bool IsAvailable => true;

    public async Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var wave = await ReadHeaderAsync(path, cancellationToken);
        return new AudioFormatInfo
        {
            Container = "WAV", Codec = wave.FormatTag == 3 ? "PCM float" : "PCM",
            SampleRate = wave.SampleRate, ChannelCount = wave.Channels, BitDepth = wave.BitsPerSample,
            BitRate = (long)wave.SampleRate * wave.Channels * wave.BitsPerSample,
            Duration = new AbsoluteTime(wave.DataLength / (decimal)wave.BlockAlign / wave.SampleRate),
            Channels = Enumerable.Range(0, wave.Channels).Select(index => new AudioChannelInfo(index, ChannelName(index, wave.Channels))).ToArray(),
        };
    }

    public async Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        var header = await ReadHeaderAsync(request.Path, cancellationToken);
        var startFrame = Math.Min((long)(request.Start.Seconds * header.SampleRate), header.DataLength / header.BlockAlign);
        var availableFrames = header.DataLength / header.BlockAlign - startFrame;
        var requestedFrames = request.Duration is null ? availableFrames : Math.Min(availableFrames, (long)(request.Duration.Value.Seconds * header.SampleRate));
        await using var stream = Open(request.Path);
        stream.Position = header.DataOffset + startFrame * header.BlockAlign;
        var bytes = new byte[FramesPerChunk * header.BlockAlign];
        long processed = 0;
        while (processed < requestedFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frames = (int)Math.Min(FramesPerChunk, requestedFrames - processed);
            await stream.ReadExactlyAsync(bytes.AsMemory(0, frames * header.BlockAlign), cancellationToken);
            await consumer(new AudioPcmChunk(ConvertSamples(bytes.AsSpan(0, frames * header.BlockAlign), header), header.SampleRate, header.Channels), cancellationToken);
            processed += frames; progress?.Report(requestedFrames == 0 ? 1 : processed / (double)requestedFrames);
        }
    }

    private static float[] ConvertSamples(ReadOnlySpan<byte> bytes, WaveHeader header)
    {
        var sampleBytes = header.BitsPerSample / 8; var result = new float[bytes.Length / sampleBytes];
        for (var index = 0; index < result.Length; index++)
        {
            var value = bytes[(index * sampleBytes)..];
            result[index] = (header.FormatTag, header.BitsPerSample) switch
            {
                (1, 8) => (value[0] - 128) / 128f,
                (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(value) / 32768f,
                (1, 24) => ReadInt24(value) / 8388608f,
                (1, 32) => BinaryPrimitives.ReadInt32LittleEndian(value) / 2147483648f,
                (3, 32) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(value)),
                _ => throw new AudioDecoderException(AudioDecodeError.UnsupportedCodec, $"WAV encoding {header.FormatTag}/{header.BitsPerSample}-bit is not supported."),
            };
        }
        return result;
    }

    private static int ReadInt24(ReadOnlySpan<byte> value)
    { var result = value[0] | value[1] << 8 | value[2] << 16; return (result & 0x800000) == 0 ? result : result | unchecked((int)0xff000000); }

    private static async Task<WaveHeader> ReadHeaderAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) throw new AudioDecoderException(AudioDecodeError.FileNotFound, $"Audio file not found: {path}");
        try
        {
            await using var stream = Open(path); var fixedHeader = new byte[12]; await stream.ReadExactlyAsync(fixedHeader, token);
            if (Encoding.ASCII.GetString(fixedHeader, 0, 4) != "RIFF" || Encoding.ASCII.GetString(fixedHeader, 8, 4) != "WAVE")
                throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The file is not a RIFF/WAVE stream.");
            ushort format = 0, channels = 0, bits = 0, align = 0; int rate = 0; long dataOffset = -1, dataLength = 0;
            var chunkHeader = new byte[8];
            while (stream.Position + 8 <= stream.Length)
            {
                await stream.ReadExactlyAsync(chunkHeader, token); var id = Encoding.ASCII.GetString(chunkHeader, 0, 4);
                var length = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(4));
                if (id == "fmt ")
                {
                    if (length < 16 || length > 1024) throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "Invalid WAV format chunk.");
                    var value = new byte[length]; await stream.ReadExactlyAsync(value, token);
                    format = BinaryPrimitives.ReadUInt16LittleEndian(value); channels = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(2));
                    rate = BinaryPrimitives.ReadInt32LittleEndian(value.AsSpan(4)); align = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(12)); bits = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(14));
                }
                else if (id == "data") { dataOffset = stream.Position; dataLength = length; stream.Position += length; }
                else stream.Position += length;
                if ((length & 1) != 0 && stream.Position < stream.Length) stream.Position++;
                if (format != 0 && dataOffset >= 0) break;
            }
            if ((format is not 1 and not 3) || channels == 0 || rate <= 0 || align == 0 || dataOffset < 0)
                throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The WAV stream has no supported format and data chunks.");
            if (dataOffset + dataLength > stream.Length) throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The WAV data chunk is truncated.");
            return new(format, channels, rate, bits, align, dataOffset, dataLength);
        }
        catch (AudioDecoderException) { throw; }
        catch (UnauthorizedAccessException exception) { throw new AudioDecoderException(AudioDecodeError.Unreadable, "The audio file is not readable.", exception); }
        catch (EndOfStreamException exception) { throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The WAV file is truncated.", exception); }
        catch (IOException exception) { throw new AudioDecoderException(AudioDecodeError.Unreadable, "The audio file could not be read.", exception); }
    }

    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private static string ChannelName(int index, int count) => count == 1 ? "Mono" : index switch { 0 => "Left", 1 => "Right", _ => $"Channel {index + 1}" };
    private sealed record WaveHeader(ushort FormatTag, ushort Channels, int SampleRate, ushort BitsPerSample, ushort BlockAlign, long DataOffset, long DataLength);
}
