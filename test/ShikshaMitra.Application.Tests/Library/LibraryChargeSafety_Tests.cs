using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryChargeSafety_Tests
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Assessment_Rejects_Nonpositive_Amounts(decimal amount)
	{
		Should.Throw<BusinessException>(() => NewCharge(amount)).Code.ShouldBe(LibraryErrorCodes.InvalidCharge);
	}

	[Fact]
	public void Assessment_Rejects_Due_Date_Before_Charge_Date()
	{
		Should.Throw<BusinessException>(() => new LibraryCharge(Guid.NewGuid(), Guid.NewGuid(), 0,
			Guid.NewGuid(), null, 20m, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(-1)))
			.Code.ShouldBe(LibraryErrorCodes.InvalidCharge);
	}

	[Theory]
	[InlineData(ChargePaymentStatus.Paid)]
	[InlineData(ChargePaymentStatus.Waived)]
	public async Task Terminal_Assessment_Cannot_Be_Edited_Or_Reopened(ChargePaymentStatus status)
	{
		var charge = NewCharge(20m);
		RestoreLegacyStatus(charge, status);
		var service = Service(charge);
		await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(charge.Id,
			new UpdateChargeRequest { Amount = 15m, PaymentStatus = (int)ChargePaymentStatus.Pending }));
		charge.Amount.ShouldBe(20m);
		charge.PaymentStatus.ShouldBe(status);
	}

	[Fact]
	public async Task Waived_Assessment_Cannot_Be_Deleted()
	{
		var charge = NewCharge(20m);
		RestoreLegacyStatus(charge, ChargePaymentStatus.Waived);
		await Should.ThrowAsync<BusinessException>(() => Service(charge).DeleteAsync(charge.Id));
	}

	[Fact]
	public void Invalid_Update_Does_Not_Partially_Mutate_The_Assessment()
	{
		var charge = NewCharge(20m);
		var due = charge.DueDate!.Value;
		Should.Throw<BusinessException>(() => charge.ApplyUpdate(15m, due.AddDays(1), ChargePaymentStatus.Paid,
			charge.ChargeDate!.Value.AddDays(-1), null, DateTime.UtcNow));
		charge.Amount.ShouldBe(20m);
		charge.DueDate.ShouldBe(due);
		charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
	}

	[Fact]
	public void DTO_Validation_Rejects_Subcent_Amounts_And_Waivers_Without_Reasons()
	{
		new CreateChargeRequestValidator().Validate(new CreateChargeRequest
		{
			MemberId = Guid.NewGuid(), ChargeCategoryId = Guid.NewGuid(), Amount = 0.001m,
			ChargeDate = DateTime.UtcNow.Date, DueDate = DateTime.UtcNow.Date.AddDays(7)
		}).IsValid.ShouldBeFalse();
		new UpdateChargeRequestValidator().Validate(new UpdateChargeRequest
		{
			PaymentStatus = (int)ChargePaymentStatus.Waived
		}).IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task Waiver_Requires_A_Recorded_Reason()
	{
		var charge = NewCharge(20m);
		await Should.ThrowAsync<BusinessException>(() => Service(charge).UpdateAsync(charge.Id,
			new UpdateChargeRequest { PaymentStatus = (int)ChargePaymentStatus.Waived }));
		charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
	}

	private static LibraryCharge NewCharge(decimal amount) => new(Guid.NewGuid(), Guid.NewGuid(), 0,
		Guid.NewGuid(), null, amount, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7), Guid.NewGuid());

	private static void RestoreLegacyStatus(LibraryCharge charge, ChargePaymentStatus status) =>
		typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(charge, status);

	private static LibraryChargeAppService Service(LibraryCharge charge) => LibraryChargeGuardFixture.Create(charge).Service;
}
