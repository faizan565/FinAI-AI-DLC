using System.Globalization;
using FinAI.Application.Ai;

namespace FinAI.Application.Insights;

public static class MonthlyInsightTemplate
{
    public static string NoData(int year, int month) =>
        $"No spending is recorded for {MonthName(year, month)}, so there is no insight for this month.";

    public static string Render(MonthlyInsightFacts facts)
    {
        var top = facts.Categories[0];
        var largest = $"{top.Category} was the largest category in {MonthName(facts.Year, facts.Month)} at {Money(top.Total)}, " +
            $"{Percent(top.Percentage)} of the {Money(facts.Total)} total.";
        return $"{largest} {Comparison(facts)}";
    }

    private static string Comparison(MonthlyInsightFacts facts)
    {
        if (facts.PreviousMonthTotal is null || facts.ChangeAmount is null)
        {
            return "No previous-month comparison is available.";
        }

        if (facts.ChangePercent is { } percent)
        {
            return percent == 0m
                ? "Total spending is unchanged from the previous month."
                : $"Total spending is {Percent(Math.Abs(percent))} {(percent > 0m ? "higher" : "lower")} than the previous month.";
        }

        return "The previous month had no recorded spending, so no percentage change is shown.";
    }

    private static string MonthName(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => amount.ToString("C", CultureInfo.InvariantCulture);

    private static string Percent(decimal value) => $"{value.ToString("0.0", CultureInfo.InvariantCulture)}%";
}
