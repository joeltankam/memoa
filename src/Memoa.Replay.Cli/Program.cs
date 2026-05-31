using System.CommandLine;
using System.CommandLine.Invocation;
using Memoa;
using Memoa.Replay;
using Memoa.Replay.Authentication;
using Memoa.Replay.Cli.Sources;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoa.Replay.Cli;

public class Program
{
    private static readonly IReplaySourceProvider[] SourceProviders =
    [
        new AzureBlobSourceProvider(),
        new FileSourceProvider(),
        new AmazonS3SourceProvider(),
        new RedisSourceProvider()
    ];

    public static async Task<int> Main(string[] args)
    {
        var sourceNames = string.Join(", ", SourceProviders.Select(static p => p.Name));

        // Core options
        var sourceOption = new Option<string>("--source", ["-s"]) { Description = $"Source backend: {sourceNames}.", Required = true };
        var targetOption = new Option<string>("--target", ["-t"]) { Description = "Base URL to replay requests against.", Required = true };

        // Timeline & pacing
        var timelineOption = new Option<string>("--timeline") { Description = "Timeline mode: none, relative.", DefaultValueFactory = _ => "none" };
        var parallelismOption = new Option<int>("--parallelism") { Description = "Number of concurrent requests (timeline=none only).", DefaultValueFactory = _ => 1 };
        var delayOption = new Option<int>("--delay") { Description = "Delay between requests in milliseconds (timeline=none only).", DefaultValueFactory = _ => 0 };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Print requests without sending them." };

        // Authentication options
        var authTokenOption = new Option<string?>("--auth-token") { Description = "Bearer token for target authentication." };
        var authHeaderOption = new Option<string?>("--auth-header") { Description = "Custom auth header in 'Name:Value' format (e.g., 'X-Api-Key:secret')." };
        var authOAuthEndpointOption = new Option<string?>("--auth-oauth-endpoint") { Description = "OAuth token endpoint URL for client credentials flow." };
        var authOAuthClientIdOption = new Option<string?>("--auth-oauth-client-id") { Description = "OAuth client ID." };
        var authOAuthClientSecretOption = new Option<string?>("--auth-oauth-client-secret") { Description = "OAuth client secret." };
        var authOAuthScopeOption = new Option<string?>("--auth-oauth-scope") { Description = "OAuth scope (e.g., 'api://my-app/.default')." };

        // Query filters
        var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Only replay requests captured after this UTC time." };
        var toOption = new Option<DateTimeOffset?>("--to") { Description = "Only replay requests captured before this UTC time." };
        var methodsOption = new Option<string[]?>("--methods") { Description = "Only replay these HTTP methods." };
        var pathPatternOption = new Option<string?>("--path") { Description = "Glob pattern to filter request paths." };

        var rootCommand = new RootCommand("Replay HTTP requests captured by Memoa middleware.")
        {
            sourceOption,
            targetOption,
            timelineOption,
            parallelismOption,
            delayOption,
            dryRunOption,
            authTokenOption,
            authHeaderOption,
            authOAuthEndpointOption,
            authOAuthClientIdOption,
            authOAuthClientSecretOption,
            authOAuthScopeOption,
            fromOption,
            toOption,
            methodsOption,
            pathPatternOption
        };

        // Register source-specific options from all providers
        foreach (var provider in SourceProviders)
        {
            foreach (var option in provider.GetOptions())
            {
                rootCommand.Add(option);
            }
        }

        rootCommand.Action = new ReplayAction(
            sourceOption, targetOption, timelineOption, parallelismOption, delayOption, dryRunOption,
            authTokenOption, authHeaderOption, authOAuthEndpointOption, authOAuthClientIdOption, authOAuthClientSecretOption, authOAuthScopeOption,
            fromOption, toOption, methodsOption, pathPatternOption);

        var config = new CommandLineConfiguration(rootCommand);
        return await config.InvokeAsync(args).ConfigureAwait(false);
    }

    private sealed class ReplayAction : AsynchronousCommandLineAction
    {
        private readonly Option<string> _source;
        private readonly Option<string> _target;
        private readonly Option<string> _timeline;
        private readonly Option<int> _parallelism;
        private readonly Option<int> _delay;
        private readonly Option<bool> _dryRun;
        private readonly Option<string?> _authToken;
        private readonly Option<string?> _authHeader;
        private readonly Option<string?> _authOAuthEndpoint;
        private readonly Option<string?> _authOAuthClientId;
        private readonly Option<string?> _authOAuthClientSecret;
        private readonly Option<string?> _authOAuthScope;
        private readonly Option<DateTimeOffset?> _from;
        private readonly Option<DateTimeOffset?> _to;
        private readonly Option<string[]?> _methods;
        private readonly Option<string?> _pathPattern;

        public ReplayAction(
            Option<string> source, Option<string> target, Option<string> timeline,
            Option<int> parallelism, Option<int> delay, Option<bool> dryRun,
            Option<string?> authToken, Option<string?> authHeader,
            Option<string?> authOAuthEndpoint, Option<string?> authOAuthClientId,
            Option<string?> authOAuthClientSecret, Option<string?> authOAuthScope,
            Option<DateTimeOffset?> from, Option<DateTimeOffset?> to,
            Option<string[]?> methods, Option<string?> pathPattern)
        {
            _source = source;
            _target = target;
            _timeline = timeline;
            _parallelism = parallelism;
            _delay = delay;
            _dryRun = dryRun;
            _authToken = authToken;
            _authHeader = authHeader;
            _authOAuthEndpoint = authOAuthEndpoint;
            _authOAuthClientId = authOAuthClientId;
            _authOAuthClientSecret = authOAuthClientSecret;
            _authOAuthScope = authOAuthScope;
            _from = from;
            _to = to;
            _methods = methods;
            _pathPattern = pathPattern;
        }

        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            var sourceName = parseResult.GetValue(_source)!;
            var target = parseResult.GetValue(_target)!;
            var timelineStr = parseResult.GetValue(_timeline)!;
            var parallelism = parseResult.GetValue(_parallelism);
            var delay = parseResult.GetValue(_delay);
            var dryRun = parseResult.GetValue(_dryRun);

            var timelineMode = timelineStr.Equals("relative", StringComparison.OrdinalIgnoreCase)
                ? TimelineMode.Relative
                : TimelineMode.None;

            var query = new RequestQuery
            {
                From = parseResult.GetValue(_from),
                To = parseResult.GetValue(_to),
                Methods = parseResult.GetValue(_methods),
                PathPattern = parseResult.GetValue(_pathPattern)
            };

            // Resolve source provider
            var provider = SourceProviders.FirstOrDefault(p => p.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                var supported = string.Join(", ", SourceProviders.Select(static p => p.Name));
                await Console.Error.WriteLineAsync($"Error: Unknown source '{sourceName}'. Supported: {supported}").ConfigureAwait(false);
                return 1;
            }

            IRequestSource requestSource;
            try
            {
                requestSource = provider.CreateSource(parseResult);
            }
            catch (InvalidOperationException ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
                return 1;
            }

            // Build authentication
            var authentication = BuildAuthentication(parseResult);

            var replayOptions = new ReplayOptions
            {
                Mode = timelineMode,
                Parallelism = parallelism,
                DelayMs = delay,
                DryRun = dryRun,
                TargetBaseUrl = target,
                Authentication = authentication
            };

            using var httpClient = new HttpClient { BaseAddress = new Uri(target) };
            var replayer = new RequestReplayer(httpClient, replayOptions, NullLogger<RequestReplayer>.Instance);

            var result = await replayer.ReplayAsync(
                requestSource.ReadAsync(query, cancellationToken),
                outcome =>
                {
                    if (dryRun)
                    {
                        Console.Out.WriteLine($"[DRY-RUN] {outcome.Request.Method} {outcome.Request.Path}{outcome.Request.QueryString ?? ""}");
                    }
                    else if (outcome.Success)
                    {
                        Console.Out.WriteLine($"[OK] {outcome.Request.Method} {outcome.Request.Path} → {outcome.StatusCode} ({outcome.Request.Id})");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[FAIL] {outcome.Request.Method} {outcome.Request.Path} ({outcome.Request.Id}): {outcome.Error}");
                    }
                },
                cancellationToken).ConfigureAwait(false);

            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Replay complete: {result.Total} total, {result.Succeeded} succeeded, {result.Failed} failed").ConfigureAwait(false);

            return result.Failed > 0 ? 1 : 0;
        }

        private ReplayAuthentication? BuildAuthentication(ParseResult parseResult)
        {
            var bearerToken = parseResult.GetValue(_authToken);
            var authHeader = parseResult.GetValue(_authHeader);
            var oauthEndpoint = parseResult.GetValue(_authOAuthEndpoint);
            var oauthClientId = parseResult.GetValue(_authOAuthClientId);
            var oauthClientSecret = parseResult.GetValue(_authOAuthClientSecret);
            var oauthScope = parseResult.GetValue(_authOAuthScope);

            // OAuth client credentials takes precedence when all required options are provided
            if (!string.IsNullOrEmpty(oauthEndpoint) && !string.IsNullOrEmpty(oauthClientId) && !string.IsNullOrEmpty(oauthClientSecret))
            {
                return new ReplayAuthentication
                {
                    OAuthClientCredentials = new Authentication.OAuthClientCredentialsOptions
                    {
                        TokenEndpoint = oauthEndpoint,
                        ClientId = oauthClientId,
                        ClientSecret = oauthClientSecret,
                        Scope = oauthScope
                    }
                };
            }

            if (!string.IsNullOrEmpty(bearerToken))
            {
                return new ReplayAuthentication { BearerToken = bearerToken };
            }

            if (!string.IsNullOrEmpty(authHeader))
            {
                var separatorIndex = authHeader.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    throw new InvalidOperationException("--auth-header must be in 'Name:Value' format (e.g., 'X-Api-Key:secret').");
                }

                return new ReplayAuthentication
                {
                    HeaderName = authHeader[..separatorIndex],
                    HeaderValue = authHeader[(separatorIndex + 1)..]
                };
            }

            return null;
        }
    }
}
