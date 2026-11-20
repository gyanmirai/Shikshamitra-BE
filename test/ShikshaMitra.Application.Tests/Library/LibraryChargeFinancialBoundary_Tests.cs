using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryChargeFinancialBoundary_Tests
{
	[Fact]
	public async Task Local_Collection_Cannot_Replace_The_Assessment_Principal_With_A_Partial_Payment()
	{
		var charge = Assessment();
		var (service, repository) = Service(charge);

		await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(charge.Id,
			new UpdateChargeRequest
			{
				Amount = 40m, PaymentStatus = (int)ChargePaymentStatus.Paid,
				PaidDate = DateTime.UtcNow, Remarks = "Partial cash payment"
			}));

		charge.Amount.ShouldBe(100m);
		charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		charge.PaidDate.ShouldBeNull();
		await repository.DidNotReceive().UpdateAsync(Arg.Any<LibraryCharge>(), Arg.Any<bool>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Library_Update_Cannot_Declare_Paid_Waived_Or_A_Paid_Date()
	{
		var commands = new[]
		{
			new UpdateChargeRequest { PaymentStatus = (int)ChargePaymentStatus.Paid },
			new UpdateChargeRequest
			{
				PaymentStatus = (int)ChargePaymentStatus.Waived, Remarks = "Principal approved waiver"
			},
			new UpdateChargeRequest
			{
				PaymentStatus = (int)ChargePaymentStatus.Paid, PaidDate = DateTime.UtcNow
			}
		};
		foreach (var command in commands)
		{
			var charge = Assessment();
			var (service, repository) = Service(charge);

			await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(charge.Id, command));

			charge.Amount.ShouldBe(100m);
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charge.PaidDate.ShouldBeNull();
			charge.Remarks.ShouldBeNull();
			await repository.DidNotReceive().UpdateAsync(Arg.Any<LibraryCharge>(), Arg.Any<bool>(),
				Arg.Any<CancellationToken>());
		}
	}

	private static LibraryCharge Assessment() => new(Guid.NewGuid(), Guid.NewGuid(), 0,
		Guid.NewGuid(), null, 100m, DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(7), Guid.NewGuid());

	private static (LibraryChargeAppService Service, IRepository<LibraryCharge, Guid> Repository) Service(
		LibraryCharge charge) => LibraryChargeGuardFixture.Create(charge);
}
