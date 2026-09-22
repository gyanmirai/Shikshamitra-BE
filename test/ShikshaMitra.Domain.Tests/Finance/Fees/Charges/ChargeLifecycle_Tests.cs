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

	private static Charge CreateCharge(ChargeStatus status) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Transport damage",
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), "Damage recovery charge", 250m, DateTimeOffset.UtcNow.AddDays(7),
		ChargePriority.Medium, status, Guid.NewGuid());
}
