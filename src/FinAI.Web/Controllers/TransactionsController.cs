using FinAI.Web.Models;
using FinAI.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Web.Controllers;

public sealed class TransactionsController(FinAiApiClient api) : FinAiControllerBase(api)
{
    private const int PageSize = 20;

    [HttpGet]
    public async Task<IActionResult> Index(TransactionFilterViewModel filter, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Transactions";
        page = Math.Max(1, page);

        var search = filter.Search?.Trim();
        var apiFilter = new ApiTransactionFilter(
            string.IsNullOrEmpty(filter.Period) ? null : filter.Period,
            filter.From is null ? null : DateOnly.FromDateTime(filter.From.Value),
            filter.To is null ? null : DateOnly.FromDateTime(filter.To.Value),
            string.IsNullOrEmpty(filter.Category) ? null : filter.Category,
            string.IsNullOrEmpty(search) ? null : search);

        var result = await Api.ListTransactionsAsync(apiFilter, page, PageSize, cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        var value = result.Value;
        var totalPages = value is null ? 1 : Math.Max(1, (int)Math.Ceiling(value.TotalCount / (double)PageSize));
        return View(new TransactionListViewModel(
            value?.Items ?? Array.Empty<ApiTransaction>(),
            page,
            totalPages,
            value?.TotalCount ?? 0,
            result.Error?.Message,
            filter));
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add transaction";
        return View("Form", new TransactionFormViewModel { Date = DateTime.UtcNow.Date });
    }

    [HttpGet]
    public IActionResult Assist()
    {
        ViewData["Title"] = "Add with AI";
        return View(new ExpenseAssistViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assist(ExpenseAssistViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add with AI";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await Api.CreateExpenseProposalAsync(model.Text.Trim(), cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        if (!result.IsSuccess)
        {
            AddApiError(result.Error!);
            return View(model);
        }

        var proposal = result.Value!;
        ViewData["Notice"] = proposal.DateIsDefaulted
            ? "Review the proposal. The date was not found in your text, so today's date is shown and needs your confirmation."
            : "Review the proposal, correct anything needed, then save.";
        return View("Form", new TransactionFormViewModel
        {
            Amount = proposal.Amount,
            Date = proposal.Date.ToDateTime(TimeOnly.MinValue),
            Description = proposal.Description,
            Category = proposal.Category,
            RequiresDateConfirmation = proposal.DateIsDefaulted
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add transaction";
        if (model.RequiresDateConfirmation && !model.DateConfirmed)
        {
            ModelState.AddModelError(nameof(model.DateConfirmed), "Confirm the date before saving.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await Api.CreateTransactionAsync(model.ToInput(), cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        if (!result.IsSuccess)
        {
            AddApiError(result.Error!);
            return View("Form", model);
        }

        TempData["Message"] = "Transaction added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Edit transaction";
        var result = await Api.GetTransactionAsync(id, cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error!.Message;
            return RedirectToAction(nameof(Index));
        }

        return View("Form", TransactionFormViewModel.FromTransaction(result.Value!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TransactionFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Edit transaction";
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await Api.UpdateTransactionAsync(id, model.ToInput(), cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        if (!result.IsSuccess)
        {
            AddApiError(result.Error!);
            return View("Form", model);
        }

        TempData["Message"] = "Transaction updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Delete transaction";
        var result = await Api.GetTransactionAsync(id, cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error!.Message;
            return RedirectToAction(nameof(Index));
        }

        return View(result.Value);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var result = await Api.DeleteTransactionAsync(id, cancellationToken);
        if (SessionExpiredRedirect(result.Error) is { } expired)
        {
            return expired;
        }

        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess
            ? "Transaction deleted."
            : result.Error!.Message;
        return RedirectToAction(nameof(Index));
    }

    private void AddApiError(ApiError error)
    {
        foreach (var (field, messages) in error.Errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (ModelState.ErrorCount == 0)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
    }
}
