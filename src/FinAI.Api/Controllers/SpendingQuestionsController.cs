using System.Security.Claims;
using FinAI.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai")]
public sealed class SpendingQuestionsController(ISpendingQuestionService questions) : ControllerBase
{
    private string? OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("spending-question")]
    public async Task<IActionResult> Ask([FromBody] SpendingQuestionRequest request, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await questions.AskAsync(ownerId, request.Text, cancellationToken);
        if (result.IsSuccess && result.Value is { Intent: SpendingIntent.Unsupported })
        {
            return Ok(new SpendingClarificationResponse(
                SpendingIntent.Unsupported.ToString(),
                "FinAI can only answer questions about your recorded spending, such as totals, a category, spending by category, or top categories for a month. It cannot predict, advise, or use external data. Try asking about a category or month, for example \"How much did I spend on food last month?\"."));
        }

        if (result.IsSuccess && result.Value is { } answer)
        {
            return Ok(new SpendingAnswerResponse(
                answer.Intent.ToString(),
                answer.Category,
                answer.From,
                answer.ToInclusive,
                answer.Total,
                answer.Categories));
        }

        return result.Failure switch
        {
            AiFailure.InvalidInput => ValidationProblem(new ValidationProblemDetails(ToErrors(result.FieldErrors))),
            AiFailure.InvalidOutput => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "FinAI could not understand that question. Try asking about a category or month, for example \"How much did I spend on food last month?\"."),
            AiFailure.RateLimited => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance is busy. Try again shortly."),
            AiFailure.Timeout => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance timed out. Try again shortly."),
            _ => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance is unavailable. Use the category and month filters instead.")
        };
    }

    private static Dictionary<string, string[]> ToErrors(IEnumerable<AiFieldError> errors) =>
        errors
            .GroupBy(error => error.Field.ToString())
            .ToDictionary(group => group.Key, group => group.Select(error => error.Code == AiFieldErrorCode.TooLong ? "The question must be 300 characters or fewer." : "Enter a question.").ToArray());
}

public sealed record SpendingQuestionRequest(string? Text);

public sealed record SpendingAnswerResponse(string Intent, string? Category, DateOnly From, DateOnly ToInclusive, decimal Total, IReadOnlyList<FinAI.Application.Transactions.CategoryTotalDto> Categories);

public sealed record SpendingClarificationResponse(string Intent, string Message);