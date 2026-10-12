using FinAI.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai")]
public sealed class AiController(IExpenseProposalService proposals) : ControllerBase
{
    [HttpPost("expense-proposal")]
    public async Task<IActionResult> ExpenseProposal([FromBody] ExpenseProposalRequest request, CancellationToken cancellationToken)
    {
        var result = await proposals.ProposeAsync(request, cancellationToken);
        if (result.IsSuccess && result.Value is { } proposal)
        {
            return Ok(proposal);
        }

        return result.Failure switch
        {
            AiFailure.InvalidInput => ValidationProblem(new ValidationProblemDetails(ToErrors(result.FieldErrors))),
            AiFailure.InvalidOutput => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "The AI response could not be used. Enter the expense manually."),
            AiFailure.RateLimited => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance is busy. Try again shortly or enter the expense manually."),
            AiFailure.Timeout => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance timed out. Try again or enter the expense manually."),
            _ => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI assistance is unavailable. Enter the expense manually.")
        };
    }

    private static Dictionary<string, string[]> ToErrors(IEnumerable<AiFieldError> errors) =>
        errors
            .GroupBy(error => error.Field.ToString())
            .ToDictionary(group => group.Key, group => group.Select(error => Describe(error.Code)).ToArray());

    private static string Describe(AiFieldErrorCode code) => code switch
    {
        AiFieldErrorCode.Missing => "This field is required.",
        AiFieldErrorCode.TooLong => "This value is too long.",
        AiFieldErrorCode.NotPositive => "Must be greater than zero.",
        AiFieldErrorCode.TooManyDecimals => "Use at most two decimal places.",
        AiFieldErrorCode.OutOfRange => "This value is out of the allowed range.",
        _ => "This value is invalid."
    };
}
