using FinAI.Application.Transactions;

namespace FinAI.Tests.Unit.Transactions;

public sealed class TransactionServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2025, 6, 1);

    private readonly FakeTransactionRepository _repository = new();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(_repository, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task CreateAsync_ValidInput_AssignsOwnerAndReturnsDto()
    {
        var result = await _sut.CreateAsync("user-1", new TransactionInput(12.50m, Today, "  Coffee  ", "food"), CancellationToken.None);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("Coffee", result.Value!.Description);
        Assert.Equal("Food", result.Value.Category);
        Assert.Single(_repository.Items);
        Assert.Equal("user-1", _repository.Items[0].UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.555)]
    public async Task CreateAsync_InvalidAmount_ReturnsInvalid(double amount)
    {
        var result = await _sut.CreateAsync("user-1", new TransactionInput((decimal)amount, Today, "Lunch", "Food"), CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey(nameof(TransactionInput.Amount)));
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task CreateAsync_DateMoreThanOneYearAhead_ReturnsInvalid()
    {
        var result = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today.AddYears(1).AddDays(1), "Lunch", "Food"), CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey(nameof(TransactionInput.Date)));
    }

    [Fact]
    public async Task CreateAsync_BlankDescription_ReturnsInvalid()
    {
        var result = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "   ", "Food"), CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey(nameof(TransactionInput.Description)));
    }

    [Fact]
    public async Task CreateAsync_UnknownCategory_ReturnsInvalid()
    {
        var result = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "Lunch", "Gambling"), CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey(nameof(TransactionInput.Category)));
    }

    [Fact]
    public async Task GetAsync_OtherUsersTransaction_ReturnsNotFound()
    {
        var created = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "Lunch", "Food"), CancellationToken.None);

        var result = await _sut.GetAsync("user-2", created.Value!.Id, CancellationToken.None);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersTransaction_ReturnsNotFoundAndLeavesDataUnchanged()
    {
        var created = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "Lunch", "Food"), CancellationToken.None);

        var result = await _sut.UpdateAsync("user-2", created.Value!.Id, new TransactionInput(99m, Today, "Hacked", "Other"), CancellationToken.None);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        Assert.Equal(5m, _repository.Items[0].Amount);
        Assert.Equal("Lunch", _repository.Items[0].Description);
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersTransaction_ReturnsNotFoundAndKeepsTransaction()
    {
        var created = await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "Lunch", "Food"), CancellationToken.None);

        var status = await _sut.DeleteAsync("user-2", created.Value!.Id, CancellationToken.None);

        Assert.Equal(ServiceStatus.NotFound, status);
        Assert.Single(_repository.Items);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyOwnersTransactions()
    {
        await _sut.CreateAsync("user-1", new TransactionInput(5m, Today, "A", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-2", new TransactionInput(6m, Today, "B", "Food"), CancellationToken.None);

        var result = await _sut.ListAsync("user-1", 1, 20, CancellationToken.None);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Single(result.Value!.Items);
        Assert.Equal("A", result.Value.Items[0].Description);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, TransactionService.MaxPageSize + 1)]
    public async Task ListAsync_InvalidPaging_ReturnsInvalid(int page, int pageSize)
    {
        var result = await _sut.ListAsync("user-1", page, pageSize, CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_ValidMonth_ReturnsTotalAndCategoryTotalsForOwnerOnly()
    {
        await _sut.CreateAsync("user-1", new TransactionInput(5m, new DateOnly(2025, 6, 2), "Lunch", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-1", new TransactionInput(7.50m, new DateOnly(2025, 6, 15), "Dinner", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-1", new TransactionInput(3m, new DateOnly(2025, 6, 20), "Bus", "Transport"), CancellationToken.None);
        await _sut.CreateAsync("user-2", new TransactionInput(100m, new DateOnly(2025, 6, 5), "Other", "Food"), CancellationToken.None);

        var result = await _sut.GetMonthlySummaryAsync("user-1", 2025, 6, CancellationToken.None);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(15.50m, result.Value!.Total);
        Assert.Equal(2, result.Value.Categories.Count);
        Assert.Contains(result.Value.Categories, c => c.Category == "Food" && c.Total == 12.50m);
        Assert.Contains(result.Value.Categories, c => c.Category == "Transport" && c.Total == 3m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_MonthBoundaries_IncludesOnlyDatesInsideMonth()
    {
        await _sut.CreateAsync("user-1", new TransactionInput(1m, new DateOnly(2025, 5, 31), "Before", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-1", new TransactionInput(2m, new DateOnly(2025, 6, 1), "First", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-1", new TransactionInput(4m, new DateOnly(2025, 6, 30), "Last", "Food"), CancellationToken.None);
        await _sut.CreateAsync("user-1", new TransactionInput(8m, new DateOnly(2025, 7, 1), "After", "Food"), CancellationToken.None);

        var result = await _sut.GetMonthlySummaryAsync("user-1", 2025, 6, CancellationToken.None);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(6m, result.Value!.Total);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_EmptyMonth_ReturnsZeroTotalAndNoCategories()
    {
        var result = await _sut.GetMonthlySummaryAsync("user-1", 2025, 6, CancellationToken.None);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(0m, result.Value!.Total);
        Assert.Empty(result.Value.Categories);
    }

    [Theory]
    [InlineData(1999, 6)]
    [InlineData(2027, 1)]
    [InlineData(2025, 0)]
    [InlineData(2025, 13)]
    public async Task GetMonthlySummaryAsync_InvalidYearOrMonth_ReturnsInvalid(int year, int month)
    {
        var result = await _sut.GetMonthlySummaryAsync("user-1", year, month, CancellationToken.None);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("month"));
    }
}
