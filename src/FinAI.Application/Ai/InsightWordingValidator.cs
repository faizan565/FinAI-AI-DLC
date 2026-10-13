namespace FinAI.Application.Ai;

public static class InsightWordingValidator
{
    public const int MaxLength = 300;

    public static AiResult<string> Validate(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength || trimmed.Any(c => char.IsDigit(c) || char.IsControl(c)))
        {
            return AiResult<string>.Fail(AiFailure.InvalidOutput);
        }

        return AiResult<string>.Success(trimmed);
    }
}
