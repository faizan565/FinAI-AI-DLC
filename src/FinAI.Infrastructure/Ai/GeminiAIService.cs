using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Application.Ai;
using FinAI.Domain.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinAI.Infrastructure.Ai;

public sealed class GeminiAIService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiAIService> logger) : IAIService
{
    private const int MaxAttempts = 3;
    private const string ApiKeyHeaderName = "x-goog-api-key";
    private static readonly TimeSpan[] RetryBackoff = [TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            amount = new { type = "NUMBER", nullable = true },
            date = new { type = "STRING", nullable = true },
            description = new { type = "STRING", nullable = true },
            category = new { type = "STRING", @enum = TransactionCategories.All }
        },
        required = new[] { "amount", "date", "description", "category" }
    };

    private readonly GeminiOptions _options = options.Value;

    public async Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.Model))
        {
            logger.LogWarning("Gemini is not configured. Set Gemini:ApiKey and Gemini:Model.");
            return Failed(AiFailure.Unavailable);
        }

        var overallTimeout = TimeSpan.FromSeconds(_options.OverallTimeoutSeconds);
        var attemptTimeout = TimeSpan.FromSeconds(_options.AttemptTimeoutSeconds);
        var clock = Stopwatch.StartNew();
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(overallTimeout);
        var body = BuildRequest(text, today);

        var result = Failed(AiFailure.Timeout);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                var backoff = RetryBackoff[attempt - 2];
                if (overallTimeout - clock.Elapsed < backoff + attemptTimeout)
                {
                    result = Failed(AiFailure.Timeout);
                    break;
                }

                await Task.Delay(backoff, cancellationToken);
            }

            var (attemptResult, retryable) = await SendAttemptAsync(body, text, today, attemptTimeout, attempt, overall.Token, cancellationToken);
            result = attemptResult;
            if (!retryable)
            {
                break;
            }
        }

        if (!result.IsSuccess)
        {
            logger.LogWarning("Gemini expense extraction failed with {Failure}", result.Failure);
        }

        return result;
    }

    private async Task<(AiResult<TransactionProposal> Result, bool Retryable)> SendAttemptAsync(
        object body,
        string text,
        DateOnly today,
        TimeSpan attemptTimeout,
        int attemptNumber,
        CancellationToken overallToken,
        CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
        attempt.CancelAfter(attemptTimeout);
        var elapsed = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            request.Headers.Add(ApiKeyHeaderName, _options.ApiKey);

            using var response = await httpClient.SendAsync(request, attempt.Token);
            logger.LogInformation("Gemini attempt {Attempt} returned HTTP {StatusCode} after {ElapsedMs} ms", attemptNumber, (int)response.StatusCode, elapsed.ElapsedMilliseconds);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return (Failed(AiFailure.RateLimited), true);
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return (Failed(AiFailure.Unavailable), true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (Failed(AiFailure.Unavailable), false);
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, attempt.Token);
            var modelText = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return (ParseProposal(modelText, text, today), false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Gemini attempt {Attempt} was cancelled after {ElapsedMs} ms ({ExceptionType})", attemptNumber, elapsed.ElapsedMilliseconds, exception.GetType().Name);
            return (Failed(AiFailure.Timeout), false);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Gemini attempt {Attempt} failed with a network error after {ElapsedMs} ms ({ExceptionType})", attemptNumber, elapsed.ElapsedMilliseconds, exception.GetType().Name);
            return (Failed(AiFailure.Unavailable), true);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning("Gemini attempt {Attempt} returned an unreadable response after {ElapsedMs} ms ({ExceptionType})", attemptNumber, elapsed.ElapsedMilliseconds, exception.GetType().Name);
            return (Failed(AiFailure.InvalidOutput), false);
        }
    }

    private static AiResult<TransactionProposal> ParseProposal(string? modelText, string text, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(modelText))
        {
            return Failed(AiFailure.InvalidOutput);
        }

        var fields = JsonSerializer.Deserialize<GeminiExpenseJson>(modelText, JsonOptions);
        if (fields is null)
        {
            return Failed(AiFailure.InvalidOutput);
        }

        var rawAmount = fields.Amount is JsonElement amount && amount.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
            ? amount.GetRawText()
            : null;

        return ExpenseProposalValidator.Validate(
            new RawExpenseFields(rawAmount, fields.Date, fields.Description, fields.Category),
            text,
            today);
    }

    private static object BuildRequest(string text, DateOnly today) => new
    {
        systemInstruction = new { parts = new[] { new { text = GeminiPrompts.SystemInstruction } } },
        contents = new[] { new { role = "user", parts = new[] { new { text = GeminiPrompts.BuildUserMessage(text, today) } } } },
        generationConfig = new { responseMimeType = "application/json", responseSchema = ResponseSchema }
    };

    private static AiResult<TransactionProposal> Failed(AiFailure failure) => AiResult<TransactionProposal>.Fail(failure);

    private sealed record GeminiExpenseJson(JsonElement? Amount, string? Date, string? Description, string? Category);

    private sealed record GeminiResponse(IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(GeminiContent? Content);

    private sealed record GeminiContent(IReadOnlyList<GeminiPart>? Parts);

    private sealed record GeminiPart(string? Text);
}
