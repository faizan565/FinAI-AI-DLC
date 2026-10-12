using FinAI.Application.Ai;

namespace FinAI.Tests.Unit.Ai;

public sealed class SpendingQuestionValidatorTests
{
    [Fact]
    public void Validate_ValidCategoryFilter_CanonicalizesCategory()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("category", "food", "CurrentMonth", null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(SpendingIntent.Category, result.Value!.Intent);
        Assert.Equal("Food", result.Value.Category);
    }

    [Fact]
    public void Validate_SpecificMonth_RequiresYearAndMonth()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Total", null, "SpecificMonth", 2025, 9, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2025, result.Value!.Year);
        Assert.Equal(9, result.Value.Month);
    }

    [Theory]
    [InlineData("Bogus", "CurrentMonth")]
    [InlineData("Total", "LastYear")]
    [InlineData(null, "CurrentMonth")]
    public void Validate_UnsupportedIntentOrPeriod_IsRejected(string? intent, string period)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields(intent, null, period, null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailure.InvalidOutput, result.Failure);
    }

    [Fact]
    public void Validate_UnknownCategory_IsRejected()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Category", "Gambling", "CurrentMonth", null, null, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_MissingCategoryForCategoryIntent_IsRejected()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Category", null, "CurrentMonth", null, null, null));

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(2025, 13)]
    [InlineData(2025, 0)]
    [InlineData(1999, 5)]
    [InlineData(null, 5)]
    [InlineData(2025, null)]
    public void Validate_SpecificMonthWithInvalidDate_IsRejected(int? year, int? month)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Total", null, "SpecificMonth", year, month, null));

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_TopCategoriesLimitOutOfRange_IsRejected(int limit)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("TopCategories", null, "CurrentMonth", null, null, limit));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_TopCategoriesWithoutLimit_UsesDefault()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("TopCategories", null, "CurrentMonth", null, null, null));

        Assert.Equal(SpendingQuestionValidator.DefaultLimit, result.Value!.Limit);
    }
}
