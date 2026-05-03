using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Memoa.Internal;

/// <summary>
/// OpenTelemetry instrumentation for Memoa: ActivitySource for traces, Meter for metrics.
/// </summary>
internal static class MemoaDiagnostics
{
    public const string ActivitySourceName = "Memoa";
    public const string MeterName = "Memoa";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetAssemblyVersion());
    public static readonly Meter Meter = new(MeterName, GetAssemblyVersion());

    // Metrics
    public static readonly Counter<long> RequestsCaptured = Meter.CreateCounter<long>(
        "memoa.requests.captured",
        description: "Total number of HTTP requests captured by Memoa.");

    public static readonly Counter<long> RequestsDropped = Meter.CreateCounter<long>(
        "memoa.requests.dropped",
        description: "Total number of captured requests dropped (channel full).");

    public static readonly Counter<long> RequestsWritten = Meter.CreateCounter<long>(
        "memoa.requests.written",
        description: "Total number of captured requests successfully written to sinks.");

    public static readonly Counter<long> RequestsFailed = Meter.CreateCounter<long>(
        "memoa.requests.failed",
        description: "Total number of captured requests that failed to write to sinks.");

    public static readonly Histogram<double> SinkWriteDuration = Meter.CreateHistogram<double>(
        "memoa.sink.write.duration",
        unit: "ms",
        description: "Duration of sink write operations in milliseconds.");

    public static readonly UpDownCounter<int> ChannelQueueSize = Meter.CreateUpDownCounter<int>(
        "memoa.channel.queue_size",
        description: "Current number of requests queued in the background channel.");

    public static readonly Counter<long> RequestsSkipped = Meter.CreateCounter<long>(
        "memoa.requests.skipped",
        description: "Total number of requests skipped by filters.");

    private static string GetAssemblyVersion()
    {
        return typeof(MemoaDiagnostics).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
