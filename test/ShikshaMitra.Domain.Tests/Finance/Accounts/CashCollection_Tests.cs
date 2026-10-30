using System;
using ShikshaMitra.Finance.Accounts.CashManagement;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class CashCollection_Tests
{
	private readonly Guid maker = Guid.NewGuid();
	private readonly Guid checker = Guid.NewGuid();
	private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
	private CashCollection Source() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"collection:source:0:", null, "R-001", "Payer", 10000m, now, maker, Guid.NewGuid(), null);

	[Fact]
	public void Direct_deposit_closes_remaining_cash_without_checker()
	{
		var cash = Source();
		cash.Spend(Guid.NewGuid(), "Expense", 2000m, now);
		cash.DepositRemaining(maker, Guid.NewGuid(), "Bank", now, "Deposit cash");
		cash.RemainingAmount.ShouldBe(0m);
		cash.DepositedAmount.ShouldBe(8000m);
		cash.Deposits.ShouldHaveSingleItem().CreditedAmount.ShouldBe(8000m);
		cash.Amount.ShouldBe(cash.SpentAmount + cash.DepositedAmount + cash.InTransitAmount + cash.RemainingAmount);
	}

	[Fact]
	public void Pool_can_spend_legacy_held_cash_and_rejects_overdraft()
	{
		var cash = LegacySource();
		var remaining = cash.RemainingAmount;
		cash.SpendAvailable(Guid.NewGuid(), "Pooled expense", 100m, now);
		cash.RemainingAmount.ShouldBe(remaining - 100m);
		Should.Throw<BusinessException>(() => cash.SpendAvailable(Guid.NewGuid(), "Overdraft", remaining, now));
	}

	[Fact]
	public void Pending_expense_and_final_net_approval_conserve_cash()
	{
		var cash = Source();
		cash.Spend(Guid.NewGuid(), "Expense", 2000m, now);
		cash.RemainingAmount.ShouldBe(8000m);
		cash.Approve(checker, Guid.NewGuid(), "School bank", 8000m, now, null);
		cash.DepositedAmount.ShouldBe(8000m);
		cash.InTransitAmount.ShouldBe(0m);
		cash.RemainingAmount.ShouldBe(0m);
		cash.Deposits.ShouldHaveSingleItem().CreditedAmount.ShouldBe(8000m);
		Should.Throw<BusinessException>(() => cash.Spend(Guid.NewGuid(), "Expense", 1m, now));
		cash.Amount.ShouldBe(10000m);
		cash.Amount.ShouldBe(cash.SpentAmount + cash.DepositedAmount + cash.InTransitAmount + cash.RemainingAmount);
	}

	[Fact]
	public void Fully_spent_pending_cash_finalizes_without_bank_deposit()
	{
		var cash = Source();
		cash.Spend(Guid.NewGuid(), "Expense", 10000m, now);
		cash.Approve(checker, Guid.NewGuid(), "School bank", 0m, now, null);
		cash.Status.ShouldBe(1);
		cash.Deposits.ShouldBeEmpty();
		cash.DepositedAmount.ShouldBe(0m);
		cash.RemainingAmount.ShouldBe(0m);
	}

	[Fact]
	public void Approval_requires_independent_checker_full_count_and_bank()
	{
		var cash = Source();
		Should.Throw<BusinessException>(() => cash.Approve(maker, Guid.NewGuid(), "bank", 10000m, now, null));
		Should.Throw<BusinessException>(() => cash.Approve(checker, Guid.NewGuid(), "bank", 9999m, now, null));
		Should.Throw<BusinessException>(() => cash.Approve(checker, Guid.Empty, "bank", 10000m, now, null));
		cash.Status.ShouldBe(0);
	}

	[Fact]
	public void Authorized_self_approval_retains_bank_count_actor_and_credit_guards()
	{
		var cash = Source();
		Should.Throw<BusinessException>(() => cash.Approve(maker, Guid.NewGuid(), "bank", 9999m, now, null, allowSelfApproval: true));
		Should.Throw<BusinessException>(() => cash.Approve(maker, Guid.Empty, "bank", 10000m, now, null, allowSelfApproval: true));
		Should.Throw<BusinessException>(() => cash.Approve(Guid.Empty, Guid.NewGuid(), "bank", 10000m, now, null, allowSelfApproval: true));
		cash.Approve(maker, Guid.NewGuid(), "bank", 10000m, now, null, allowSelfApproval: true);
		cash.Status.ShouldBe(1);
		cash.ApprovedBy.ShouldBe(maker);
		cash = LegacySource();
		var deposit = cash.Dispatch(Guid.NewGuid(), maker, 100m, now, "slip", null);
		Should.Throw<BusinessException>(() => cash.Credit(deposit.Id, maker, 100m, now, "statement", null));
	}

	[Fact]
	public void Overdraft_precision_self_credit_and_credit_before_dispatch_are_rejected()
	{
		var cash = Source();
		Should.Throw<BusinessException>(() => cash.Spend(Guid.NewGuid(), "Expense", 10001m, now));
		Should.Throw<BusinessException>(() => cash.Dispatch(Guid.NewGuid(), maker, 0.001m, now, "slip", null));
		cash = LegacySource();
		var deposit = cash.Dispatch(Guid.NewGuid(), maker, 100m, now, "slip", null);
		Should.Throw<BusinessException>(() => cash.Credit(deposit.Id, maker, 100m, now, "statement", null));
		Should.Throw<BusinessException>(() => cash.Credit(deposit.Id, checker, 101m, now, "statement", null));
		Should.Throw<BusinessException>(() => cash.Credit(deposit.Id, checker, 100m, now.AddDays(-1), "statement", null));
	}

	[Fact]
	public void Retry_fingerprint_and_stamp_guard_commands()
	{
		var cash = Source();
		var key = Guid.NewGuid();
		cash.SaveCommand(key, "approve-body", "result");
		cash.Replay(key, "approve-body").ShouldBe("result");
		Should.Throw<BusinessException>(() => cash.Replay(key, "changed-body"));
		Should.Throw<BusinessException>(() => cash.EnsureStamp("stale"));
		cash.EnsureStamp(cash.ConcurrencyStamp);
	}

	[Fact]
	public void Manual_cash_income_retains_manual_classification_and_approval_binds_bank()
	{
		var transaction = new ShikshaMitra.Finance.Accounts.Incomes.Transaction(Guid.NewGuid(), null, Guid.NewGuid(),
			Guid.NewGuid(), "Donation", null, 100m, 0, now.UtcDateTime);
		transaction.LinkCashCollection(Guid.NewGuid(), pendingIncome: true);
		transaction.IsAutomatic.ShouldBeFalse();
		transaction.IsConfirmed.ShouldBeFalse();
		Should.Throw<BusinessException>(() => transaction.Update(Guid.NewGuid(), "Changed", null, 1m, 0, now.UtcDateTime, null));
		var bankId = Guid.NewGuid();
		transaction.ApproveCashIncome(bankId, "School bank • 1234");
		transaction.IsConfirmed.ShouldBeTrue();
		transaction.BankAccountId.ShouldBe(bankId);
		transaction.BankLabelSnapshot.ShouldBe("School bank • 1234");
		Should.Throw<BusinessException>(() => transaction.ApproveCashIncome(bankId, "School bank • 1234"));
	}
	[Theory]
	[InlineData(0)]
	[InlineData(80)]
	public void Cash_income_finalization_preserves_original_gross_and_cannot_be_reapplied(decimal net)
	{
		var transaction = new ShikshaMitra.Finance.Accounts.Incomes.Transaction(Guid.NewGuid(), null, Guid.NewGuid(),
			Guid.NewGuid(), "Donation", null, 100m, 0, now.UtcDateTime);
		transaction.LinkCashCollection(Guid.NewGuid(), pendingIncome: true);
		transaction.ApproveCashIncome(Guid.NewGuid(), "School bank", net);
		transaction.Amount.ShouldBe(net);
		transaction.GetOriginalCashIncomeAmount().ShouldBe(100m);
		transaction.IsConfirmed.ShouldBe(net > 0);
		Should.Throw<BusinessException>(() => transaction.ApproveCashIncome(Guid.NewGuid(), "School bank", net));
	}

	[Fact]
	public void Net_income_allocation_is_exact_bounded_and_deterministic_in_paise()
	{
		CashIncomeAllocator.Allocate(new[] { .01m, .01m, .01m, .01m }, .02m)
			.ShouldBe(new[] { .01m, .01m, 0m, 0m });
		CashIncomeAllocator.Allocate(new[] { 33.33m, 33.33m, 33.34m }, 80m)
			.ShouldBe(new[] { 26.67m, 26.66m, 26.67m });
		CashIncomeAllocator.Allocate(new[] { 33.33m, 33.33m, 33.34m }, 0m)
			.ShouldBe(new[] { 0m, 0m, 0m });
		Should.Throw<BusinessException>(() => CashIncomeAllocator.Allocate(new[] { 1m }, 1.01m));
	}

	private CashCollection LegacySource()
	{
		// Persisted history predates final-deposit approval; new approval must not recreate this state.
		var cash = Source();
		typeof(CashCollection).GetProperty(nameof(CashCollection.Status))!.SetValue(cash, 1);
		return cash;
	}

}
