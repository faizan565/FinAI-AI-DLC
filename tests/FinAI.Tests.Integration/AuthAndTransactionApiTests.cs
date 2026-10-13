using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Application.Transactions;
using FinAI.Tests.Integration.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinAI.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AuthAndTransactionApiTests
{
    private const string CsrfHeader = "X-CSRF-TOKEN";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;

    public AuthAndTransactionApiTests(ApiTestFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Register_ThenLogin_WithValidCredentials_ReturnsUser()
    {
        var userName = UniqueUserName();
        using var client = NewClient();
        var token = await GetCsrfTokenAsync(client);

        var register = await PostJsonAsync(client, "/api/v1/auth/register", new { userName, password = Password }, token);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await PostJsonAsync(client, "/api/v1/auth/login", new { userName, password = Password }, token);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(userName, (await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGeneric401()
    {
        var userName = await RegisterUserAsync();
        using var client = NewClient();
        var token = await GetCsrfTokenAsync(client);

        var response = await PostJsonAsync(client, "/api/v1/auth/login", new { userName, password = "WrongPassword!1" }, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Invalid username or password.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutAuthentication_Return401()
    {
        using var client = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/transactions", ValidTransaction())).StatusCode);
    }

    [Fact]
    public async Task StateChangingRequest_WithoutAntiforgeryToken_IsRejected()
    {
        var userName = await RegisterUserAsync();
        using var client = NewClient();
        var token = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/api/v1/auth/login", new { userName, password = Password }, token);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", ValidTransaction());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransactionLifecycle_CreateListGetUpdateDelete_ForAuthenticatedOwner()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var create = await PostJsonAsync(client, "/api/v1/transactions", ValidTransaction(), await CsrfAsync(client));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.NotNull(created);

        var list = await client.GetFromJsonAsync<PagedResult<TransactionDto>>("/api/v1/transactions", JsonOptions);
        Assert.Contains(list!.Items, t => t.Id == created!.Id);

        var get = await client.GetAsync($"/api/v1/transactions/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await SendJsonAsync(HttpMethod.Put, client, $"/api/v1/transactions/{created.Id}", ValidTransaction(amount: 42.00m), await CsrfAsync(client));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var delete = await SendJsonAsync(HttpMethod.Delete, client, $"/api/v1/transactions/{created.Id}", null, await CsrfAsync(client));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Transactions_OfAnotherUser_CannotBeReadUpdatedOrDeleted()
    {
        var owner = await RegisterUserAsync();
        var other = await RegisterUserAsync();

        using var ownerClient = await LoginAsync(owner);
        var create = await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(), await CsrfAsync(ownerClient));
        var created = await create.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        using var otherClient = await LoginAsync(other);
        var otherCsrf = await CsrfAsync(otherClient);
        var url = $"/api/v1/transactions/{created!.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJsonAsync(HttpMethod.Put, otherClient, url, ValidTransaction(amount: 1m), otherCsrf)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJsonAsync(HttpMethod.Delete, otherClient, url, null, otherCsrf)).StatusCode);

        var stillThere = await ownerClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_InvalidInput_ReturnsValidationProblem()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var response = await PostJsonAsync(client, "/api/v1/transactions", ValidTransaction(amount: -5m, category: "Gambling"), await CsrfAsync(client));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyDashboard_WithoutAuthentication_Returns401()
    {
        using var client = NewClient();

        var response = await client.GetAsync($"/api/v1/dashboard/monthly?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyDashboard_ReturnsOnlyAuthenticatedUsersTotalsForMonth()
    {
        var owner = await RegisterUserAsync();
        var other = await RegisterUserAsync();
        using var ownerClient = await LoginAsync(owner);
        using var otherClient = await LoginAsync(other);

        await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(amount: 10.25m, category: "Food"), await CsrfAsync(ownerClient));
        await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(amount: 4.75m, category: "Transport"), await CsrfAsync(ownerClient));
        await PostJsonAsync(otherClient, "/api/v1/transactions", ValidTransaction(amount: 500m, category: "Food"), await CsrfAsync(otherClient));

        var now = DateTime.UtcNow;
        var summary = await ownerClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/dashboard/monthly?year={now.Year}&month={now.Month}", JsonOptions);

        Assert.Equal(15m, summary.GetProperty("total").GetDecimal());
        var categories = summary.GetProperty("categories").EnumerateArray().ToList();
        Assert.Equal(2, categories.Count);
        Assert.Contains(categories, c => c.GetProperty("category").GetString() == "Food" && c.GetProperty("total").GetDecimal() == 10.25m);
        Assert.Contains(categories, c => c.GetProperty("category").GetString() == "Transport" && c.GetProperty("total").GetDecimal() == 4.75m);
    }

    [Theory]
    [InlineData(2025, 13)]
    [InlineData(1999, 1)]
    public async Task MonthlyDashboard_InvalidYearOrMonth_Returns400(int year, int month)
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var response = await client.GetAsync($"/api/v1/dashboard/monthly?year={year}&month={month}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListTransactions_CombinedFilters_ReturnsOnlyMatchingOwnerRecordsWithFilteredCount()
    {
        var owner = await RegisterUserAsync();
        var other = await RegisterUserAsync();
        using var ownerClient = await LoginAsync(owner);
        using var otherClient = await LoginAsync(other);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(amount: 3m, category: "Food", date: today, description: "Morning coffee"), await CsrfAsync(ownerClient));
        await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(amount: 4m, category: "Food", date: today.AddDays(-1), description: "Coffee yesterday"), await CsrfAsync(ownerClient));
        await PostJsonAsync(ownerClient, "/api/v1/transactions", ValidTransaction(amount: 5m, category: "Transport", date: today, description: "Coffee bus"), await CsrfAsync(ownerClient));
        await PostJsonAsync(otherClient, "/api/v1/transactions", ValidTransaction(amount: 9m, category: "Food", date: today, description: "Coffee other"), await CsrfAsync(otherClient));

        var result = await ownerClient.GetFromJsonAsync<PagedResult<TransactionDto>>(
            $"/api/v1/transactions?period=Today&category=Food&search=coffee&page=1&pageSize=20", JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalCount);
        Assert.Equal("Morning coffee", result.Items.Single().Description);
    }

    [Fact]
    public async Task ListTransactions_CustomRangeStartAfterEnd_Returns400()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var response = await client.GetAsync("/api/v1/transactions?period=CustomRange&from=2025-06-10&to=2025-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListTransactions_FilteredPaging_TotalCountReflectsFilter()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
        {
            await PostJsonAsync(client, "/api/v1/transactions", ValidTransaction(category: "Food", date: today, description: $"Lunch {i}"), await CsrfAsync(client));
        }
        await PostJsonAsync(client, "/api/v1/transactions", ValidTransaction(category: "Health", date: today, description: "Clinic"), await CsrfAsync(client));

        var result = await client.GetFromJsonAsync<PagedResult<TransactionDto>>(
            "/api/v1/transactions?category=Food&page=2&pageSize=2", JsonOptions);

        Assert.Equal(3, result!.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListTransactions_NoMatches_ReturnsEmptyPage()
    {
        var userName = await RegisterUserAsync();
        using var client = await LoginAsync(userName);

        var result = await client.GetFromJsonAsync<PagedResult<TransactionDto>>(
            "/api/v1/transactions?search=does-not-exist", JsonOptions);

        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static readonly string Password = "Str0ng-Passw0rd!";

    private static object ValidTransaction(decimal amount = 12.50m, string category = "Food", DateOnly? date = null, string description = "Lunch") =>
        new { amount, date = date ?? DateOnly.FromDateTime(DateTime.UtcNow), description, category };

    private static string UniqueUserName() => $"user_{Guid.NewGuid():N}";

    private async Task<string> RegisterUserAsync()
    {
        var userName = UniqueUserName();
        using var client = NewClient();
        var token = await GetCsrfTokenAsync(client);
        var response = await PostJsonAsync(client, "/api/v1/auth/register", new { userName, password = Password }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return userName;
    }

    private async Task<HttpClient> LoginAsync(string userName)
    {
        var client = NewClient();
        var token = await GetCsrfTokenAsync(client);
        var response = await PostJsonAsync(client, "/api/v1/auth/login", new { userName, password = Password }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    private async Task<string> CsrfAsync(HttpClient client) => await GetCsrfTokenAsync(client);

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", JsonOptions);
        return response.GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body, string? csrfToken) =>
        SendJsonAsync(HttpMethod.Post, client, url, body, csrfToken);

    private static Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, HttpClient client, string url, object? body, string? csrfToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (csrfToken is not null)
        {
            request.Headers.Add(CsrfHeader, csrfToken);
        }

        return client.SendAsync(request);
    }
}
