using System.Text.Json;

namespace VoidNote.Application.Diagnostics;

public enum DiagnosticState { Available, Missing, Failed, NotApplicable }

public sealed record DiagnosticCheck(string Id, string Name, DiagnosticState State, string Summary, string? Version = null, string? Path = null, string? Guidance = null);

public sealed record VoidNoteDiagnosticReport(DateTimeOffset CreatedAtUtc, string ApplicationVersion, IReadOnlyList<DiagnosticCheck> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });

    public string ToText() => string.Join(Environment.NewLine,
        new[] { $"VoidNote Diagnostics {ApplicationVersion}", $"Created (UTC): {CreatedAtUtc:O}", string.Empty }
            .Concat(Checks.Select(value => $"[{value.State}] {value.Name}: {value.Summary}{(value.Version is null ? string.Empty : $" (version {value.Version})")}")));
}

public interface IVoidNoteDiagnosticsService
{
    Task<VoidNoteDiagnosticReport> RunAsync(CancellationToken cancellationToken = default);
}
