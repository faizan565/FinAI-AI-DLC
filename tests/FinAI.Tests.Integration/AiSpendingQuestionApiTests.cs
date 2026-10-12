using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Application.Ai;
using FinAI.Tests.Integration.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinAI.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AiSpendingQuestionApiTests
{
    private const string CsrfHeader = "X-CSRF-TOKEN";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng-Passw0rd!";

    private readonly ApiFactory _factory;
    private readonly FakeAiService _ai;

    public AiSpendingQuestionApiTests(ApiTestFixture fixture)
    {
        _factory = fixture.Factory;
        _ai = fixture.Factory.AiService;
    }

    [Fact]
    public async Task SpendingQuestion_WithoutAuthentication_Returns401()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/ai/spending-question", new { text = "How much on food?" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SpendingQuestion_ValidAnswer_SerializesIntentAsString()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        _ai.SpendingFilter = AiResult<SpendingQuestionFilter>.Success(
            new SpendingQuestionFilter(SpendingIntent.Total, null, SpendingPeriod.CurrentMonth, null, null, null));

        var response = await PostJsonAsync(client, "/api/v1/ai/spending-question", new { text = "How much did I spend this month?" }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var intent = body.GetProperty("intent");
        Assert.Equal(JsonValueKind.String, intent.ValueKind);
        Assert.Equal("Total", intent.GetString());
        Assert.Equal(0m, body.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task SpendingQuestion_Unsupported_ReturnsClarificationWithoutTotal()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        _ai.SpendingFilter = AiResult<SpendingQuestionFilter>.Success(
            new SpendingQuestionFilter(SpendingIntent.Unsupported, null, SpendingPeriod.CurrentMonth, null, null, null));

        var response = await PostJsonAsync(client, "/api/v1/ai/spending-question", new { text = "How much will I spend next month?" }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Unsupported", body.GetProperty("intent").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.False(body.TryGetProperty("total", out _));
        Assert.False(body.TryGetProperty("categories", out _));
    }

    [Fact]
    public async Task SpendingQuestion_EmptyPeriod_ReturnsEmptyCategoriesArray()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        _ai.SpendingFilter = AiResult<SpendingQuestionFilter>.Success(
            new SpendingQuestionFilter(SpendingIntent.ByCategory, null, SpendingPeriod.PreviousMonth, null, null, null));

        var response = await PostJsonAsync(client, "/api/v1/ai/spending-question", new { text = "spending by category last month" }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("categories").ValueKind);
        Assert.Equal(0, body.GetProperty("categories").GetArrayLength());
    }

    private async Task<string> RegisterUserAsync()
    {
        using var client = NewClient();
        var userName = $"ask-{Guid.NewGuid():N}@example.com";
        var response = await PostJsonAsync(client, "/api/v1/auth/register", new { userName, password = Password }, await CsrfAsync(client));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return userName;
    }

    private async Task<HttpClient> LoginAsync(string userName)
    {
        var client = NewClient();
        var response = await PostJsonAsync(client, "/api/v1/auth/login", new { userName, password = Password }, await CsrfAsync(client));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", JsonOptions);
        return token.GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: JsonOptions) };
        request.Headers.Add(CsrfHeader, csrf);
        return client.SendAsync(request);
    }

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });
}
