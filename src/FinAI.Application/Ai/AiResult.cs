namespace FinAI.Application.Ai;

public enum AiFailure
{
    Unavailable,
    Timeout,
    InvalidOutput,
    RateLimited,
    InvalidInput
}

public enum AiField
{
    Text,
    Amount,
    Date,
    Description,
    Category
}

public enum AiFieldErrorCode
{
    Missing,
    Invalid,
    NotPositive,
    TooManyDecimals,
    OutOfRange,
    TooLong
}

public sealed record AiFieldError(AiField Field, AiFieldErrorCode Code);

public sealed record AiResult<T>(T? Value, AiFailure? Failure, IReadOnlyList<AiFieldError> FieldErrors)
{
    public bool IsSuccess => Failure is null;

    public static AiResult<T> Success(T value) => new(value, null, Array.Empty<AiFieldError>());

    public static AiResult<T> Fail(AiFailure failure) => new(default, failure, Array.Empty<AiFieldError>());

    public static AiResult<T> Invalid(AiFailure failure, IReadOnlyList<AiFieldError> fieldErrors) => new(default, failure, fieldErrors);
}
