namespace FinAI.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int AttemptTimeoutSeconds { get; set; } = 3;

    public int OverallTimeoutSeconds { get; set; } = 10;
}
