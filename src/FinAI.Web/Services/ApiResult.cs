namespace FinAI.Web.Services;

public sealed record ApiError(int StatusCode, string Message, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsUnauthorized => StatusCode == 401;

    public bool IsValidation => StatusCode == 400;

    public string FirstMessage => Errors.Values.SelectMany(messages => messages).FirstOrDefault() ?? Message;
}

public sealed record ApiResult<T>(T? Value, ApiError? Error)
{
    public bool IsSuccess => Error is null;
}
