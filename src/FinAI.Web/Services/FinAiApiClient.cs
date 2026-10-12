using System.Net;
using System.Text.Json;
using FinAI.Web.Models;

namespace FinAI.Web.Services;

public sealed class FinAiApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    public const string AuthCookieName = "FinAI.Auth";
    public const string CsrfCookieName = "FinAI.Csrf";

    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private const string SetCookieHeaderName = "Set-Cookie";
    private static readonly string[] ApiCookieNames = [AuthCookieName, CsrfCookieName];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("No active HTTP request.");

    public bool IsSignedIn => Session.GetString(AuthCookieName) is not null;

    public void ForgetSession() => Session.Clear();

    public Task<ApiResult<ApiUser>> RegisterAsync(string userName, string password, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiUser>(HttpMethod.Post, "api/v1/auth/register", new ApiCredentials(userName, password), requiresCsrf: true, cancellationToken);

    public Task<ApiResult<ApiUser>> LoginAsync(string userName, string password, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiUser>(HttpMethod.Post, "api/v1/auth/login", new ApiCredentials(userName, password), requiresCsrf: true, cancellationToken);

    public Task<ApiResult<bool>> LogoutAsync(CancellationToken cancellationToken) =>
        ExecuteAsync<bool>(HttpMethod.Post, "api/v1/auth/logout", null, requiresCsrf: true, cancellationToken);

    public Task<ApiResult<ApiMonthlySummary>> GetMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiMonthlySummary>(HttpMethod.Get, $"api/v1/dashboard/monthly?year={year}&month={month}", null, requiresCsrf: false, cancellationToken);

    public Task<ApiResult<ApiPagedResult<ApiTransaction>>> ListTransactionsAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiPagedResult<ApiTransaction>>(HttpMethod.Get, $"api/v1/transactions?page={page}&pageSize={pageSize}", null, requiresCsrf: false, cancellationToken);

    public Task<ApiResult<ApiTransaction>> GetTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiTransaction>(HttpMethod.Get, $"api/v1/transactions/{id}", null, requiresCsrf: false, cancellationToken);

    public Task<ApiResult<ApiTransaction>> CreateTransactionAsync(ApiTransactionInput input, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiTransaction>(HttpMethod.Post, "api/v1/transactions", input, requiresCsrf: true, cancellationToken);

    public Task<ApiResult<ApiExpenseProposal>> CreateExpenseProposalAsync(string text, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiExpenseProposal>(HttpMethod.Post, "api/v1/ai/expense-proposal", new ApiExpenseProposalRequest(text), requiresCsrf: true, cancellationToken);

    public Task<ApiResult<ApiTransaction>> UpdateTransactionAsync(Guid id, ApiTransactionInput input, CancellationToken cancellationToken) =>
        ExecuteAsync<ApiTransaction>(HttpMethod.Put, $"api/v1/transactions/{id}", input, requiresCsrf: true, cancellationToken);

    public Task<ApiResult<bool>> DeleteTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<bool>(HttpMethod.Delete, $"api/v1/transactions/{id}", null, requiresCsrf: true, cancellationToken);

    private async Task<ApiResult<T>> ExecuteAsync<T>(HttpMethod method, string path, object? body, bool requiresCsrf, CancellationToken cancellationToken)
    {
        try
        {
            var csrfToken = requiresCsrf ? await FetchCsrfTokenAsync(cancellationToken) : null;
            if (requiresCsrf && csrfToken is null)
            {
                return Unavailable<T>();
            }

            using var response = await SendAsync(method, path, body, csrfToken, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(default, await ReadErrorAsync(response, cancellationToken));
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new ApiResult<T>(default, null);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value is null ? Unavailable<T>() : new ApiResult<T>(value, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return Unavailable<T>();
        }
    }

    private async Task<string?> FetchCsrfTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/auth/csrf", null, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<ApiCsrfToken>(JsonOptions, cancellationToken);
        return string.IsNullOrEmpty(token?.RequestToken) ? null : token.RequestToken;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? csrfToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (csrfToken is not null)
        {
            request.Headers.Add(CsrfHeaderName, csrfToken);
        }

        var cookieHeader = BuildCookieHeader();
        if (cookieHeader is not null)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        StoreCookies(response);
        return response;
    }

    private string? BuildCookieHeader()
    {
        var pairs = ApiCookieNames
            .Select(name => (name, value: Session.GetString(name)))
            .Where(cookie => cookie.value is not null)
            .Select(cookie => $"{cookie.name}={cookie.value}")
            .ToList();

        return pairs.Count == 0 ? null : string.Join("; ", pairs);
    }

    private void StoreCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(SetCookieHeaderName, out var setCookieHeaders))
        {
            return;
        }

        foreach (var header in setCookieHeaders)
        {
            var pair = header.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator];
            if (!ApiCookieNames.Contains(name))
            {
                continue;
            }

            var value = pair[(separator + 1)..];
            if (value.Length == 0)
            {
                Session.Remove(name);
            }
            else
            {
                Session.SetString(name, value);
            }
        }
    }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        string? title = null;

        // Error bodies are optional, so fall back to status-based messages when they are missing.
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            if (problem.ValueKind == JsonValueKind.Object)
            {
                if (problem.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                {
                    title = titleElement.GetString();
                }

                if (problem.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errorsElement.EnumerateObject())
                    {
                        errors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.EnumerateArray().Select(message => message.GetString() ?? string.Empty).ToArray()
                            : Array.Empty<string>();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
        }

        var status = (int)response.StatusCode;
        return new ApiError(status, title ?? DefaultMessage(status), errors);
    }

    private static string DefaultMessage(int status) => status switch
    {
        401 => "Your session has expired. Please sign in again.",
        403 => "You do not have permission to perform this action.",
        404 => "The requested item could not be found.",
        409 => "That change conflicts with existing data.",
        _ => "Something went wrong. Please try again."
    };

    private static ApiResult<T> Unavailable<T>() =>
        new(default, new ApiError(503, "The FinAI service is unavailable. Please try again.", new Dictionary<string, string[]>()));
}
