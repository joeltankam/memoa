using Memoa;
using Serilog;
using Serilog.Events;

namespace Serilog.Sinks.Memoa;

/// <summary>
/// Memoa <see cref="IRequestSink"/> that writes captured requests as structured Serilog log events.
/// Requests flow through the Memoa pipeline and are emitted to whatever Serilog sinks are configured.
/// </summary>
public sealed class MemoaSerilogSink : IRequestSink
{
    private readonly ILogger _logger;
    private readonly MemoaSinkOptions _options;

    public MemoaSerilogSink(ILogger logger, MemoaSinkOptions? options = null)
    {
        _logger = logger.ForContext<MemoaSerilogSink>();
        _options = options ?? new MemoaSinkOptions();
    }

    public ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        if (!_logger.IsEnabled(_options.Level))
        {
            return ValueTask.CompletedTask;
        }

        var logEvent = CreateLogEvent(request);
        _logger.Write(logEvent);

        return ValueTask.CompletedTask;
    }

    private LogEvent CreateLogEvent(RecordedRequest request)
    {
        var properties = new List<LogEventProperty>
        {
            new("RequestId", new ScalarValue(request.Id)),
            new("Method", new ScalarValue(request.Method)),
            new("Path", new ScalarValue(request.Path)),
            new("QueryString", new ScalarValue(request.QueryString)),
            new("Scheme", new ScalarValue(request.Scheme)),
            new("Host", new ScalarValue(request.Host)),
            new("CapturedAtUtc", new ScalarValue(request.CapturedAtUtc)),
            new("CorrelationId", new ScalarValue(request.CorrelationId))
        };

        if (request.Response is not null)
        {
            properties.Add(new("StatusCode", new ScalarValue(request.Response.StatusCode)));
            properties.Add(new("ElapsedMs", new ScalarValue(request.Response.ElapsedMs)));

            if (_options.IncludeResponseBody && request.Response.Body is not null)
            {
                properties.Add(new("ResponseBody", new ScalarValue(
                    TruncateBody(request.Response.Body.Text))));
                properties.Add(new("ResponseContentType", new ScalarValue(
                    request.Response.Body.ContentType)));
            }
        }

        if (_options.IncludeRequestBody && request.Body is not null)
        {
            properties.Add(new("RequestBody", new ScalarValue(TruncateBody(request.Body.Text))));
            properties.Add(new("RequestContentType", new ScalarValue(request.Body.ContentType)));
            properties.Add(new("RequestBodyLength", new ScalarValue(request.Body.Length)));
        }

        if (_options.IncludeHeaders && request.Headers is not null)
        {
            var headerDict = new Dictionary<ScalarValue, LogEventPropertyValue>();
            foreach (var (key, values) in request.Headers)
            {
                headerDict[new ScalarValue(key)] = new ScalarValue(string.Join(", ", values));
            }

            properties.Add(new("Headers", new DictionaryValue(headerDict)));
        }

        if (request.ClientIp is not null)
        {
            properties.Add(new("ClientIp", new ScalarValue(request.ClientIp)));
        }

        var messageTemplate = new Serilog.Parsing.MessageTemplateParser();

        var template = messageTemplate.Parse("HTTP {Method} {Path} captured");

        return new LogEvent(
            request.CapturedAtUtc,
            _options.Level,
            exception: null,
            template,
            properties);
    }

    private string? TruncateBody(string? body)
    {
        if (body is null)
        {
            return null;
        }

        if (body.Length <= _options.MaxBodyLength)
        {
            return body;
        }

        return body[.._options.MaxBodyLength] + "...[truncated]";
    }
}
