using System;
using System.Linq;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class IncomeExpenseRouting_Tests
{
    [Fact]
    public void Published_collection_policy_retains_the_late_fee_category_through_snapshot_round_trip()
    {
        var categoryId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var model = new CollectionPolicyUpdateModelBuilder().Build() with { LateFeeIncomeCategoryId = categoryId };
        var policy = new CollectionPolicy(policyId, null, yearId, model);
        var snapshot = CollectionPolicySnapshot.Deserialize(policy.ToSnapshot().Serialize());
        snapshot.LateFeeIncomeCategoryId.ShouldBe(categoryId);
        CollectionPolicy.FromSnapshot(policyId, null, yearId, 1, snapshot).LateFeeIncomeCategoryId.ShouldBe(categoryId);
    }

    [Fact]
    public void Split_payments_preserve_every_category_and_instrument_cent()
    {
        decimal[] categories = [100.01m, 50.02m, 0.03m];
        decimal[] instruments = [20.01m, 110.02m, 20.03m];
        var result = FinanceIncomeAllocator.Allocate(categories, instruments);
        for (var i = 0; i < categories.Length; i++)
            result.Where(x => x.CategoryIndex == i).Sum(x => x.Amount).ShouldBe(categories[i]);
        for (var i = 0; i < instruments.Length; i++)
            result.Where(x => x.InstrumentIndex == i).Sum(x => x.Amount).ShouldBe(instruments[i]);
        result.All(x => x.Amount > 0 && decimal.Round(x.Amount, 2) == x.Amount).ShouldBeTrue();
    }

    [Theory]
    [InlineData("1.005", "1.005")]
    [InlineData("0", "0")]
    [InlineData("1.00", "0.99")]
    public void Invalid_money_or_unbalanced_allocations_are_rejected(string category, string instrument)
    {
        Should.Throw<BusinessException>(() => FinanceIncomeAllocator.Allocate(
            [decimal.Parse(category, System.Globalization.CultureInfo.InvariantCulture)],
            [decimal.Parse(instrument, System.Globalization.CultureInfo.InvariantCulture)]));
    }

    [Fact]
    public void Pending_income_stays_hidden_until_payment_confirmation()
    {
        var entry = Entry(false);
        entry.IsConfirmed.ShouldBeFalse();
        entry.Confirm();
        entry.IsConfirmed.ShouldBeTrue();
        entry.Amount.ShouldBe(12.34m);
        entry.CategoryNameSnapshot.ShouldBe("School fees");
    }

    [Fact]
    public void Source_owned_entries_cannot_be_edited_as_manual_income()
    {
        var entry = Entry(true);
        entry.IsAutomatic.ShouldBeTrue();
        Should.Throw<BusinessException>(() => entry.Update(Guid.NewGuid(), "Altered", null,
            1m, 0, DateTime.UtcNow, null)).Code.ShouldBe("Finance:AutomaticEntryImmutable");
    }

    [Fact]
    public void Returned_expense_is_a_signed_compensation_with_its_original_category()
    {
        var entry = Entry(true, true, TransactionType.Expense);
        entry.Amount.ShouldBe(-12.34m);
        entry.TransactionType.ShouldBe(TransactionType.Expense);
        entry.CategoryNameSnapshot.ShouldBe("School fees");
        entry.SourceId.ShouldNotBeNull();
    }

    private static Transaction Entry(bool confirmed, bool correction = false, TransactionType type = TransactionType.Income) => Transaction.Automatic(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School fees",
        Guid.NewGuid(), "School bank • 1234", "Receipt-123", 12.34m, 0, DateTime.UtcNow,
        type, "collection:test", Guid.NewGuid(), Guid.NewGuid(), confirmed, correction);
}
