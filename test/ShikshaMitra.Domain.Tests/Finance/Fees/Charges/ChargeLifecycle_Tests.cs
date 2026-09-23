using System;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ChargeLifecycle_Tests
{
	[Fact]
	public void Approved_charge_can_be_voided_with_audited_reason()
	{
		var charge = CreateCharge(ChargeStatus.Approved);
		var userId = Guid.NewGuid();

		charge.Void(userId, "Duplicate charge raised in error");

		charge.Status.ShouldBe(ChargeStatus.Voided);
		charge.VoidedBy.ShouldBe(userId);
		charge.VoidedOn.ShouldNotBeNull();
		charge.VoidReason.ShouldBe("Duplicate charge raised in error");
	}

	[Fact]
	public void Pending_charge_cannot_be_voided()
	{
		var charge = CreateCharge(ChargeStatus.Pending);

		var exception = Should.Throw<BusinessException>(() =>
			charge.Void(Guid.NewGuid(), "Duplicate charge raised in error"));

		exception.Code.ShouldBe(FinanceChargeErrorCodes.ChargeCannotBeVoided);
	}

	[Fact]
	public void Accounting_mapping_is_snapshotted_on_charge_line()
	{
		var charge = CreateCharge(ChargeStatus.Pending);
		var categoryId = Guid.NewGuid();
		var accountHeadId = Guid.NewGuid();

		charge.SetAccountingSnapshot(categoryId, accountHeadId);

		charge.IncomeCategoryId.ShouldBe(categoryId);
		charge.IncomeAccountHeadId.ShouldBe(accountHeadId);
	}

	[Fact]
	public void Charge_rejects_amount_with_more_than_two_decimal_places()
	{
		var exception = Should.Throw<BusinessException>(() => CreateCharge(ChargeStatus.Pending, 1.005m));

		exception.Code.ShouldBe(FinanceChargeErrorCodes.InvalidChargeAmountScale);
	}

	[Fact]
	public void Charge_rejects_amount_above_supported_maximum()
	{
		var exception = Should.Throw<BusinessException>(() =>
			CreateCharge(ChargeStatus.Pending, FinanceChargeAmountRules.MaximumAmount + 0.01m));

		exception.Code.ShouldBe(FinanceChargeErrorCodes.ChargeAmountExceedsMaximum);
	}

	private static Charge CreateCharge(ChargeStatus status, decimal amount = 250m) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Transport damage",
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), "Damage recovery charge", amount, DateTimeOffset.UtcNow.AddDays(7),
		ChargePriority.Medium, status, Guid.NewGuid());
}
