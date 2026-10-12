using System.Net;
using System.Net.Http.Json;
using FinAI.Application.Ai;
using FinAI.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinAI.Tests.Unit.Ai;

public sealed class GeminiAIServiceTests
{
    private static readonly DateOnly Today = new(2025, 6, 15);
    private const string InputText = "I spent 2500 rupees on lunch today.";
    private const string ValidModelJson = """{"amount": 2500, "date": "2025-06-15", "description": "Lunch", "category": "Food"}""";

    [Fact]
    public async Task ExtractTransactionAsync_ValidResponse_ReturnsValidatedProposal()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(GeminiReply(ValidModelJson)));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new TransactionProposal(2500m, Today, "Lunch", "Food", false), result.Value);
        Assert.Equal("test-key", handler.ApiKeyHeader);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_MalformedModelJson_ReturnsInvalidOutput_WithoutRetry()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(GeminiReply("not json at all")));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.InvalidOutput, result.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_AmountAsString_ReturnsInvalidAmount()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(GeminiReply("""{"amount": "2500", "date": "2025-06-15", "description": "Lunch", "category": "Food"}""")));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.InvalidOutput, result.Failure);
        Assert.Contains(new AiFieldError(AiField.Amount, AiFieldErrorCode.Invalid), result.FieldErrors);
    }

    [Fact]
    public async Task ExtractTransactionAsync_NotConfigured_ReturnsUnavailable_WithoutCallingGemini()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(GeminiReply(ValidModelJson)));
        var service = CreateService(handler, apiKey: string.Empty);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.Unavailable, result.Failure);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_Timeouts_ReturnTimeoutWithoutRetry()
    {
        var handler = new ScriptedHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.Timeout, result.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_RateLimited_ReturnsRateLimitedAfterThreeAttempts()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.RateLimited, result.Failure);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_ApiRejectsRequest_ReturnsUnavailable_WithoutRetry()
    {
        var handler = new ScriptedHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.Unavailable, result.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExtractTransactionAsync_NetworkError_ReturnsUnavailableAfterThreeAttempts()
    {
        var handler = new ScriptedHandler(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("network")));
        var service = CreateService(handler);

        var result = await service.ExtractTransactionAsync(InputText, Today, CancellationToken.None);

        Assert.Equal(AiFailure.Unavailable, result.Failure);
        Assert.Equal(3, handler.Calls);
    }

    private static GeminiAIService CreateService(ScriptedHandler handler, string apiKey = "test-key")
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "test-model" });
        return new GeminiAIService(http, options, NullLogger<GeminiAIService>.Instance);
    }

    private static HttpResponseMessage GeminiReply(string modelText) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { candidates = new[] { new { content = new { parts = new[] { new { text = modelText } } } } } })
    };

    private sealed class ScriptedHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? ApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            ApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.FirstOrDefault() : null;
            return respond(request);
        }
    }
}
