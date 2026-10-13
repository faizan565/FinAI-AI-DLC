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
    public void Validate_CategoryYesterday_IsAccepted()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Category", "food", "Yesterday", null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(SpendingIntent.Category, result.Value!.Intent);
        Assert.Equal("Food", result.Value.Category);
        Assert.Equal(SpendingPeriod.Yesterday, result.Value.Period);
    }

    [Fact]
    public void Validate_TotalYesterday_IsAccepted()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("Total", null, "Yesterday", null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(SpendingIntent.Total, result.Value!.Intent);
        Assert.Equal(SpendingPeriod.Yesterday, result.Value.Period);
    }

    [Fact]
    public void Validate_UnsupportedIntentWithYesterday_IsRejected()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("TopCategories", null, "Yesterday", null, null, null));

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("Total", "Last7Days", null)]
    [InlineData("Total", "PreviousWeek", null)]
    [InlineData("Category", "Last7Days", "food")]
    [InlineData("Category", "PreviousWeek", "Food")]
    public void Validate_RollingAndWeekPeriods_AcceptTotalAndCategory(string intent, string period, string? category)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields(intent, category, period, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(Enum.Parse<SpendingPeriod>(period), result.Value!.Period);
    }

    [Theory]
    [InlineData("ByCategory", "Last7Days")]
    [InlineData("TopCategories", "PreviousWeek")]
    public void Validate_RollingAndWeekPeriods_RejectOtherIntents(string intent, string period)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields(intent, null, period, null, null, null));

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("Unsupported", "PreviousWeek")]
    public void Validate_UnsupportedIntent_ReturnsUnsupportedFilter(string intent, string period)
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields(intent, null, period, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(SpendingIntent.Unsupported, result.Value!.Intent);
    }

    [Fact]
    public void Validate_TopCategoriesWithoutLimit_UsesDefault()
    {
        var result = SpendingQuestionValidator.Validate(new RawSpendingQuestionFields("TopCategories", null, "CurrentMonth", null, null, null));

        Assert.Equal(SpendingQuestionValidator.DefaultLimit, result.Value!.Limit);
    }
}
