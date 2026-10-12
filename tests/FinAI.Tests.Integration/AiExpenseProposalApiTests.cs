using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Application.Ai;
using FinAI.Tests.Integration.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinAI.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AiExpenseProposalApiTests
{
    private const string CsrfHeader = "X-CSRF-TOKEN";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng-Passw0rd!";

    private readonly ApiFactory _factory;
    private readonly FakeAiService _ai;

    public AiExpenseProposalApiTests(ApiTestFixture fixture)
    {
        _factory = fixture.Factory;
        _ai = fixture.Factory.AiService;
    }

    [Fact]
    public async Task ExpenseProposal_WithoutAuthentication_Returns401()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/ai/expense-proposal", new { text = "Lunch 2500" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseProposal_ValidResult_ReturnsProposalWithoutSavingTransaction()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        var proposal = new TransactionProposal(2500m, DateOnly.FromDateTime(DateTime.UtcNow), "Lunch", "Food", false);
        _ai.Result = AiResult<TransactionProposal>.Success(proposal);

        var response = await PostJsonAsync(client, "/api/v1/ai/expense-proposal", new { text = "I spent 2500 rupees on lunch today." }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2500m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("Food", body.GetProperty("category").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/transactions", JsonOptions);
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ExpenseProposal_EmptyText_Returns400()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var response = await PostJsonAsync(client, "/api/v1/ai/expense-proposal", new { text = "   " }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseProposal_TextOver500Characters_Returns400()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var response = await PostJsonAsync(client, "/api/v1/ai/expense-proposal", new { text = new string('a', 501) }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseProposal_AiUnavailable_Returns503WithoutDetails()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        _ai.Result = AiResult<TransactionProposal>.Fail(AiFailure.Timeout);

        var response = await PostJsonAsync(client, "/api/v1/ai/expense-proposal", new { text = "Lunch 2500" }, await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });

    private async Task<string> RegisterUserAsync()
    {
        var userName = $"user_{Guid.NewGuid():N}";
        using var client = NewClient();
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
}
