using System.Globalization;
using FinAI.Application.Transactions;
using FinAI.Domain.Transactions;

namespace FinAI.Application.Ai;

public static class ExpenseProposalValidator
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string UncategorizedCategory = "Uncategorized";
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public static AiResult<TransactionProposal> Validate(RawExpenseFields fields, string inputText, DateOnly today)
    {
        var errors = new List<AiFieldError>();
        var amount = ParseAmount(fields.AmountJson, errors);
        var date = ParseDate(fields.Date, today, out var dateIsDefaulted, errors);
        var description = ParseDescription(fields.Description, inputText, errors);
        var category = TransactionCategories.TryNormalize(fields.Category, out var canonical) ? canonical : UncategorizedCategory;

        return errors.Count > 0
            ? AiResult<TransactionProposal>.Invalid(AiFailure.InvalidOutput, errors)
            : AiResult<TransactionProposal>.Success(new TransactionProposal(amount, date, description, category, dateIsDefaulted));
    }

    private static decimal ParseAmount(string? amountJson, List<AiFieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(amountJson) || amountJson == "null")
        {
            errors.Add(new AiFieldError(AiField.Amount, AiFieldErrorCode.Missing));
            return 0m;
        }

        if (!decimal.TryParse(amountJson, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount))
        {
            errors.Add(new AiFieldError(AiField.Amount, AiFieldErrorCode.Invalid));
            return 0m;
        }

        if (amount <= 0m)
        {
            errors.Add(new AiFieldError(AiField.Amount, AiFieldErrorCode.NotPositive));
        }
        else if (amount > MaxAmount)
        {
            errors.Add(new AiFieldError(AiField.Amount, AiFieldErrorCode.OutOfRange));
        }
        else if (decimal.Round(amount, 2) != amount)
        {
            errors.Add(new AiFieldError(AiField.Amount, AiFieldErrorCode.TooManyDecimals));
        }

        return amount;
    }

    private static DateOnly ParseDate(string? date, DateOnly today, out bool isDefaulted, List<AiFieldError> errors)
    {
        if (!DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            isDefaulted = true;
            return today;
        }

        isDefaulted = false;
        if (parsed > today.AddYears(1))
        {
            errors.Add(new AiFieldError(AiField.Date, AiFieldErrorCode.OutOfRange));
        }

        return parsed;
    }

    private static string ParseDescription(string? description, string inputText, List<AiFieldError> errors)
    {
        var value = string.IsNullOrWhiteSpace(description) ? inputText : description.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new AiFieldError(AiField.Description, AiFieldErrorCode.Missing));
            return string.Empty;
        }

        if (value.Length > TransactionService.DescriptionMaxLength)
        {
            errors.Add(new AiFieldError(AiField.Description, AiFieldErrorCode.TooLong));
            return string.Empty;
        }

        return value;
    }
}
