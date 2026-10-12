using FinAI.Application.Ai;

namespace FinAI.Tests.Unit.Ai;

public sealed class ExpenseProposalValidatorTests
{
    private static readonly DateOnly Today = new(2025, 6, 15);
    private const string InputText = "I spent 2500 rupees on lunch today.";

    [Fact]
    public void Validate_ValidFields_ReturnsProposal()
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2025-06-15", "Lunch", "Food"), InputText, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(new TransactionProposal(2500m, Today, "Lunch", "Food", false), result.Value);
    }

    [Theory]
    [InlineData(null, AiFieldErrorCode.Missing)]
    [InlineData("\"14.50\"", AiFieldErrorCode.Invalid)]
    [InlineData("abc", AiFieldErrorCode.Invalid)]
    [InlineData("0", AiFieldErrorCode.NotPositive)]
    [InlineData("-5", AiFieldErrorCode.NotPositive)]
    [InlineData("14.555", AiFieldErrorCode.TooManyDecimals)]
    public void Validate_InvalidAmount_ReturnsFieldErrorWithoutProposal(string? amountJson, AiFieldErrorCode code)
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields(amountJson, "2025-06-15", "Lunch", "Food"), InputText, Today);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(new AiFieldError(AiField.Amount, code), result.FieldErrors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("14/06/2025")]
    [InlineData("2025-6-1")]
    [InlineData("2025-06-15T10:00:00")]
    public void Validate_MissingOrUnparseableDate_DefaultsToTodayAndMarksDefaulted(string? date)
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", date, "Lunch", "Food"), InputText, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value!.Date);
        Assert.True(result.Value.DateIsDefaulted);
    }

    [Fact]
    public void Validate_DateMoreThanOneYearAhead_ReturnsOutOfRange()
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2026-07-01", "Lunch", "Food"), InputText, Today);

        Assert.False(result.IsSuccess);
        Assert.Contains(new AiFieldError(AiField.Date, AiFieldErrorCode.OutOfRange), result.FieldErrors);
    }

    [Fact]
    public void Validate_DescriptionOver200Characters_ReturnsTooLongWithoutTruncating()
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2025-06-15", new string('a', 201), "Food"), InputText, Today);

        Assert.False(result.IsSuccess);
        Assert.Contains(new AiFieldError(AiField.Description, AiFieldErrorCode.TooLong), result.FieldErrors);
    }

    [Fact]
    public void Validate_MissingDescription_UsesInputTextWhenWithinLimit()
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2025-06-15", null, "Food"), InputText, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(InputText, result.Value!.Description);
    }

    [Fact]
    public void Validate_MissingDescriptionAndLongInput_ReturnsTooLong()
    {
        var longInput = new string('b', 201);

        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2025-06-15", null, "Food"), longInput, Today);

        Assert.False(result.IsSuccess);
        Assert.Contains(new AiFieldError(AiField.Description, AiFieldErrorCode.TooLong), result.FieldErrors);
    }

    [Theory]
    [InlineData("Gambling", "Uncategorized")]
    [InlineData(null, "Uncategorized")]
    [InlineData("food", "Food")]
    public void Validate_CategoryOutsideAllowedList_MapsToUncategorizedOrCanonicalName(string? category, string expected)
    {
        var result = ExpenseProposalValidator.Validate(new RawExpenseFields("2500", "2025-06-15", "Lunch", category), InputText, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value!.Category);
    }
}
