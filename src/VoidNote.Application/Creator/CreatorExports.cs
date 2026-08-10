using System.Globalization;
using System.Text;
using System.Text.Json;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Music;

namespace VoidNote.Application.Creator;

public sealed record CreatorSessionReport(int TrackCount, int TakeCount, int Completed, int Pending, int NeedsRetake,
    int SectionCount, AbsoluteTime TotalDuration, CreatorSyncSettings SyncSettings,
    IReadOnlyList<string> Shawzins, int AvailableCodes);

public interface ICreatorExportService
{
    string ExportJson(CreatorSession session, int? framesPerSecond = null);
    string ExportCsv(CreatorSession session, int? framesPerSecond = null);
    byte[] ExportSyncWave(CreatorSession session, int sampleRate = 48000);
    CreatorSessionReport CreateReport(CreatorSession session);
}

public sealed class CreatorExportService(ICreatorTimingService timing) : ICreatorExportService
{
    public string ExportJson(CreatorSession session, int? framesPerSecond = null) => JsonSerializer.Serialize(
        Rows(session, framesPerSecond), new JsonSerializerOptions { WriteIndented = true });

    public string ExportCsv(CreatorSession session, int? framesPerSecond = null)
    {
        var frame = framesPerSecond is null ? "" : $",MusicStartFrame{framesPerSecond},SyncFrame{framesPerSecond}";
        var builder = new StringBuilder("TakeName,Attempt,PreRoll,CountInStart,SyncPoint,MusicStart,MusicEnd,PostRollEnd,SourceStart" + frame + "\r\n");
        foreach (var row in Rows(session, framesPerSecond))
        {
            builder.Append(Escape(row.TakeName)).Append(',').Append(row.Attempt).Append(',').Append(Seconds(row.PreRoll)).Append(',')
                .Append(Seconds(row.CountInStart)).Append(',').Append(Seconds(row.SyncPoint)).Append(',').Append(Seconds(row.MusicStart)).Append(',')
                .Append(Seconds(row.MusicEnd)).Append(',').Append(Seconds(row.PostRollEnd)).Append(',').Append(Seconds(row.SourceStart));
            if (framesPerSecond is not null) builder.Append(',').Append(row.MusicStartFrame).Append(',').Append(row.SyncFrame);
            builder.Append("\r\n");
        }
        return builder.ToString();
    }

    public byte[] ExportSyncWave(CreatorSession session, int sampleRate = 48000)
    {
        if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var first = session.Takes.FirstOrDefault() ?? throw new InvalidOperationException("A sync wave needs at least one take.");
        var plan = timing.Plan(session, first); var length = checked((int)decimal.Ceiling(plan.Markers.MusicStart.Seconds * sampleRate) + sampleRate / 2);
        var samples = new short[length]; var countStart = plan.Markers.CountInStart.Seconds;
        var beatSeconds = (plan.Markers.SyncPoint.Seconds - countStart - session.SyncSettings.ClickCount * session.SyncSettings.ClickInterval.Seconds) / plan.CountInBeats;
        for (var index = 0; index < plan.CountInBeats; index++) Tone(samples, sampleRate, countStart + index * beatSeconds, 1000, 0.04m, 0.45m);
        var syncStart = plan.Markers.SyncPoint.Seconds - session.SyncSettings.ClickCount * session.SyncSettings.ClickInterval.Seconds;
        for (var index = 0; index < session.SyncSettings.ClickCount; index++) Tone(samples, sampleRate, syncStart + index * session.SyncSettings.ClickInterval.Seconds, 1400, 0.04m, 0.55m);
        Tone(samples, sampleRate, plan.Markers.SyncPoint.Seconds, 240, 0.12m, 0.95m);
        if (session.SyncSettings.IncludeMusicStartMarkerInWave) Tone(samples, sampleRate, plan.Markers.MusicStart.Seconds, 1800, 0.025m, 0.35m);
        return Wave(samples, sampleRate);
    }

    public CreatorSessionReport CreateReport(CreatorSession session) => new(
        session.Takes.Select(value => value.SourceTrackId).Distinct().Count(), session.Takes.Count,
        session.Takes.Count(value => value.Status == CreatorTakeStatus.Completed),
        session.Takes.Count(value => value.Status is CreatorTakeStatus.Pending or CreatorTakeStatus.Ready or CreatorTakeStatus.Recording),
        session.Takes.Count(value => value.Status == CreatorTakeStatus.NeedsRetake), session.Sections.Count,
        new(session.Takes.Select(value => timing.Plan(session, value).Markers.PostRollEnd.Seconds).DefaultIfEmpty(0m).Max()),
        session.SyncSettings, session.Takes.Select(value => value.Instrument).Where(value => value.Length > 0).Distinct().Order().ToArray(),
        session.Takes.Count(value => !string.IsNullOrWhiteSpace(value.SongCode)));

    private IReadOnlyList<SyncRow> Rows(CreatorSession session, int? fps) => session.Takes.OrderBy(value => value.CreatedAt).ThenBy(value => value.AttemptNumber).Select(take =>
    {
        var marker = timing.Plan(session, take).Markers;
        return new SyncRow(take.Name, take.AttemptNumber, session.SyncSettings.PreRoll, marker.CountInStart, marker.SyncPoint, marker.MusicStart,
            marker.MusicEnd, marker.PostRollEnd, marker.SourceStart, fps is null ? null : timing.ToFrame(marker.MusicStart, fps.Value),
            fps is null ? null : timing.ToFrame(marker.SyncPoint, fps.Value));
    }).ToArray();
    private static string Seconds(AbsoluteTime value) => value.Seconds.ToString("0.#########", CultureInfo.InvariantCulture);
    private static string Escape(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static void Tone(short[] samples, int rate, decimal at, int frequency, decimal duration, decimal gain)
    {
        var start = Math.Max(0, checked((int)decimal.Round(at * rate, 0, MidpointRounding.AwayFromZero)));
        var count = checked((int)decimal.Round(duration * rate, 0, MidpointRounding.AwayFromZero));
        for (var i = 0; i < count && start + i < samples.Length; i++)
        {
            var envelope = 1d - i / (double)count;
            var value = Math.Sin(2d * Math.PI * frequency * i / rate) * envelope * (double)gain * short.MaxValue;
            samples[start + i] = (short)Math.Clamp(samples[start + i] + value, short.MinValue, short.MaxValue);
        }
    }
    private static byte[] Wave(short[] samples, int rate)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2); foreach (var sample in samples) writer.Write(sample); writer.Flush(); return stream.ToArray();
    }
    private sealed record SyncRow(string TakeName, int Attempt, AbsoluteTime PreRoll, AbsoluteTime CountInStart, AbsoluteTime SyncPoint,
        AbsoluteTime MusicStart, AbsoluteTime MusicEnd, AbsoluteTime PostRollEnd, AbsoluteTime SourceStart, int? MusicStartFrame, int? SyncFrame);
}
