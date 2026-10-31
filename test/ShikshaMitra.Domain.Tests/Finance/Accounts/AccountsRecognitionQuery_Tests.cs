using System;
using System.Linq;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountsRecognitionQuery_Tests
{
    [Theory]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Expense)]
    public void None_routing_evidence_is_excluded_without_changing_payment_confirmation(TransactionType type)
    {
        var year = Guid.NewGuid();
        var none = new TransactionCategory(Guid.NewGuid(), null, year, type, "None", null);
        var regular = new TransactionCategory(Guid.NewGuid(), null, year, type, "School fees", null);
        var entries = new[]
        {
            Entry(none.Id, "None", 10m, type),
            Entry(none.Id, null, 20m, type),
            Entry(regular.Id, " none ", 30m, type),
            Entry(regular.Id, "School fees", 40m, type),
        };

        var recognized = AccountsRecognitionQuery.ExcludeNone(entries.AsQueryable(), new[] { none, regular }.AsQueryable()).ToArray();

        recognized.ShouldHaveSingleItem().Amount.ShouldBe(40m);
        entries.All(x => x.IsConfirmed).ShouldBeTrue();
        entries.Sum(x => x.Amount).ShouldBe(100m);
    }

    [Fact]
    public void Legacy_noncash_manual_entry_can_update_its_category_snapshot_without_a_bank()
    {
        var entry = new Transaction(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Legacy", null,
            10m, 3, DateTime.UtcNow);
        entry.Update(Guid.NewGuid(), "Legacy", null, 10m, 3, DateTime.UtcNow, null, "None");
        entry.CategoryNameSnapshot.ShouldBe("None");
        entry.BankAccountId.ShouldBeNull();
        AccountsRecognitionQuery.ExcludeNone(new[] { entry }.AsQueryable(), Array.Empty<TransactionCategory>().AsQueryable())
            .ShouldBeEmpty();
    }

    private static Transaction Entry(Guid category, string? label, decimal amount, TransactionType type) => Transaction.Automatic(
        Guid.NewGuid(), null, Guid.NewGuid(), category, label!, Guid.NewGuid(), "School bank", "Receipt",
        amount, 3, DateTime.UtcNow, type, Guid.NewGuid().ToString(), Guid.NewGuid(), null);
}
