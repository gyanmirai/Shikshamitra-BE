using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private static readonly string[] FineReportAmountFields =
	[
		"originalAmountText", "acceptedAmountText", "settledAmountText", "pendingVerificationAmountText",
		"netWaivedAmountText", "financialOutstandingAmountText", "collectibleAmountText", "refundedAmountText"
	];

	private Task SeedFineReportLegacyDeclarationsAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var template = await repository.GetAsync(owner.AssessmentId);
		// Stored Staff2 is legitimate legacy Employee evidence. The public Staff1
		// filter includes it without relabeling the stored code as Student0.
		var paid = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 2, template.ChargeCategoryId, null,
			20.25m, new DateTime(2024, 6, 10), new DateTime(2024, 6, 24), owner.TenantId);
		var waived = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, template.ChargeCategoryId, null,
			30.50m, new DateTime(2024, 6, 11), new DateTime(2024, 6, 25), owner.TenantId);
		// Initial legacy declarations, before first persistence; no payment API,
		// receipt, Finance row or edit of any previously bound source.
		typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(paid, ChargePaymentStatus.Paid);
		typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(waived, ChargePaymentStatus.Waived);
		paid.AcademicYearId.ShouldBeNull(); paid.Currency.ShouldBeNull(); paid.FinanceSourceLinkId.ShouldBeNull();
		paid.MemberRole.ShouldBe(2);
		waived.AcademicYearId.ShouldBeNull(); waived.Currency.ShouldBeNull(); waived.FinanceSourceLinkId.ShouldBeNull();
		await repository.InsertManyAsync([paid, waived], true);
	});
}
