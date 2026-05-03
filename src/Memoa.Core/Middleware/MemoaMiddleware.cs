using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Internal;

/// <summary>
/// ASP.NET Core middleware that captures HTTP requests and delivers them to registered sinks.
/// </summary>
internal sealed class MemoaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRequestPipeline _pipeline;
    private readonly ILogger<MemoaMiddleware> _logger;
    private readonly IOptionsMonitor<MemoaOptions> _optionsMonitor;

    public MemoaMiddleware(
        RequestDelegate next,
        IRequestPipeline pipeline,
        ILogger<MemoaMiddleware> logger,
        IOptionsMonitor<MemoaOptions> optionsMonitor)
    {
        _next = next;
        _pipeline = pipeline;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var requestPath = context.Request.Path.Value ?? "/";

        // Apply filters before any allocation
        var pathFilter = new PathFilter(options.Filters.PathIncludePatterns, options.Filters.PathExcludePatterns);
        if (!pathFilter.ShouldInclude(requestPath))
        {
            MemoaDiagnostics.RequestsSkipped.Add(1);
            await _next(context).ConfigureAwait(false);
            return;
        }

        var method = context.Request.Method;
        if (options.Filters.Methods.Count > 0 &&
            !options.Filters.Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            MemoaDiagnostics.RequestsSkipped.Add(1);
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Apply sampling — evaluated after filters so excluded paths don't consume the sample budget
        if (options.Sampling.Rate < 1.0 && Random.Shared.NextDouble() >= options.Sampling.Rate)
        {
            MemoaDiagnostics.RequestsSkipped.Add(1);
            await _next(context).ConfigureAwait(false);
            return;
        }

        using var activity = MemoaDiagnostics.ActivitySource.StartActivity("memoa.capture");

        var requestId = Guid.NewGuid();
        var capturedAt = DateTimeOffset.UtcNow;

        activity?.SetTag("memoa.request.id", requestId.ToString());
        activity?.SetTag("http.method", method);
        activity?.SetTag("http.path", requestPath);

        // Extract correlation ID
        string? correlationId = null;
        if (!string.IsNullOrEmpty(options.CorrelationIdHeader) &&
            context.Request.Headers.TryGetValue(options.CorrelationIdHeader, out var correlationValues))
        {
            correlationId = correlationValues.FirstOrDefault();
            activity?.SetTag("memoa.correlation_id", correlationId);
        }

        // Capture request body
        RecordedBody? requestBody = null;
        if (options.Capture.IncludeBody && context.Request.ContentLength is > 0)
        {
            context.Request.EnableBuffering();
            requestBody = await CaptureRequestBodyAsync(context.Request, options.Capture).ConfigureAwait(false);
        }

        // Capture headers
        IReadOnlyDictionary<string, string[]>? headers = null;
        if (options.Capture.IncludeHeaders)
        {
            headers = CaptureHeaders(context.Request.Headers, options.Capture);
        }

        // Setup response capture if needed
        ResponseCaptureStream? responseCaptureStream = null;
        Stream? originalBodyStream = null;
        var sw = Stopwatch.StartNew();

        if (options.Capture.IncludeResponse)
        {
            originalBodyStream = context.Response.Body;
            responseCaptureStream = new ResponseCaptureStream(
                originalBodyStream, options.Capture.MaxResponseBodySizeBytes);
            context.Response.Body = responseCaptureStream;
        }

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();

            if (responseCaptureStream is not null && originalBodyStream is not null)
            {
                context.Response.Body = originalBodyStream;
            }
        }

        // Build recorded response if enabled
        RecordedResponse? recordedResponse = null;
        if (options.Capture.IncludeResponse)
        {
            recordedResponse = BuildRecordedResponse(
                context.Response, responseCaptureStream, sw.Elapsed.TotalMilliseconds, options.Capture);

            if (responseCaptureStream is not null)
            {
                await responseCaptureStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        // Capture route values
        IReadOnlyDictionary<string, string?>? routeValues = null;
        if (options.Capture.IncludeRouteValues)
        {
            var routeData = context.GetRouteData();
            if (routeData.Values.Count > 0)
            {
                routeValues = routeData.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString());
            }
        }

        var recordedRequest = new RecordedRequest
        {
            Id = requestId,
            CapturedAtUtc = capturedAt,
            CorrelationId = correlationId,
            Method = method,
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.Value ?? string.Empty,
            PathBase = context.Request.PathBase.Value,
            Path = requestPath,
            QueryString = options.Capture.IncludeQueryString ? context.Request.QueryString.Value : null,
            Protocol = context.Request.Protocol,
            RouteValues = routeValues,
            Headers = headers,
            ClientIp = options.Capture.IncludeClientIp ? context.Connection.RemoteIpAddress?.ToString() : null,
            Body = requestBody,
            Response = recordedResponse
        };

        MemoaDiagnostics.RequestsCaptured.Add(1);

        try
        {
            await _pipeline.SubmitAsync(recordedRequest, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to submit captured request {RequestId} to pipeline", requestId);
        }
    }

    private static async Task<RecordedBody?> CaptureRequestBodyAsync(HttpRequest request, MemoaCaptureOptions options)
    {
        var classifier = new ContentTypeClassifier(options.BinaryContentTypePatterns);
        var contentType = request.ContentType;
        var isBinary = classifier.IsBinary(contentType);

        request.Body.Position = 0;
        var maxSize = options.MaxBodySizeBytes;
        var buffer = new byte[Math.Min(maxSize + 1, 81920)];
        using var ms = new MemoryStream();
        int bytesRead;
        var totalRead = 0;

        while ((bytesRead = await request.Body.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var toWrite = Math.Min(bytesRead, maxSize - totalRead);
            if (toWrite > 0)
            {
                await ms.WriteAsync(buffer.AsMemory(0, toWrite)).ConfigureAwait(false);
            }

            totalRead += bytesRead;
            if (totalRead > maxSize)
            {
                break;
            }
        }

        var truncated = totalRead > maxSize;
        var data = ms.ToArray();
        var bodyLength = request.ContentLength ?? totalRead;

        // Reset position for downstream middleware
        request.Body.Position = 0;

        if (isBinary)
        {
            return new RecordedBody
            {
                ContentType = contentType,
                Length = bodyLength,
                Base64Bytes = Convert.ToBase64String(data),
                Truncated = truncated
            };
        }

        return new RecordedBody
        {
            ContentType = contentType,
            Length = bodyLength,
            Text = System.Text.Encoding.UTF8.GetString(data),
            Truncated = truncated
        };
    }

    private static Dictionary<string, string[]> CaptureHeaders(
        IHeaderDictionary requestHeaders, MemoaCaptureOptions options)
    {
        var filter = new HeaderFilter(options.HeaderAllowList, options.HeaderDenyList);
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in requestHeaders)
        {
            if (filter.ShouldInclude(header.Key))
            {
                result[header.Key] = header.Value.Select(v => v ?? string.Empty).ToArray();
            }
        }

        return result;
    }

    private static RecordedResponse BuildRecordedResponse(
        HttpResponse response,
        ResponseCaptureStream? captureStream,
        double elapsedMs,
        MemoaCaptureOptions options)
    {
        var responseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var headerFilter = new HeaderFilter(options.HeaderAllowList, options.HeaderDenyList);

        foreach (var header in response.Headers)
        {
            if (headerFilter.ShouldInclude(header.Key))
            {
                responseHeaders[header.Key] = header.Value.Select(v => v ?? string.Empty).ToArray();
            }
        }

        RecordedBody? responseBody = null;
        if (options.IncludeResponseBody && captureStream is not null)
        {
            var data = captureStream.GetCapturedBytes();
            var classifier = new ContentTypeClassifier(options.BinaryContentTypePatterns);
            var contentType = response.ContentType;

            if (classifier.IsBinary(contentType))
            {
                responseBody = new RecordedBody
                {
                    ContentType = contentType,
                    Length = data.Length,
                    Base64Bytes = Convert.ToBase64String(data),
                    Truncated = captureStream.Truncated
                };
            }
            else
            {
                responseBody = new RecordedBody
                {
                    ContentType = contentType,
                    Length = data.Length,
                    Text = System.Text.Encoding.UTF8.GetString(data),
                    Truncated = captureStream.Truncated
                };
            }
        }

        return new RecordedResponse
        {
            StatusCode = response.StatusCode,
            Headers = responseHeaders,
            Body = responseBody,
            ElapsedMs = elapsedMs
        };
    }
}
