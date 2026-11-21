using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Canonical_Consumer_Position_Should_Report_Refunded_Money_Without_Reopening_Debt()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(fixture.Administrator);
		await SeedApprovedRefundStateAsync(fixture);
		var paid = await WithUnitOfWorkAsync(() => DownstreamRefundService(fixture.Administrator)
			.RecordPayoutAsync(fixture.Submitted.Id, RecordedTypedPayout(fixture)));
		paid.Status.ShouldBe(FeeRefundStatus.Processed);
		paid.TotalAmount.ShouldBe(15.25m); paid.CalculatedAmount.ShouldBe(15m); paid.ProcessedAmount.ShouldBe(15m);
		var row = await ConsumerPositionAsync(fixture.Administrator);
		row.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		row.Refunded.ShouldBe(15m); row.Accepted.ShouldBe(40.25m); row.Settled.ShouldBe(40.25m);
		row.Original.ShouldBe(100.75m); row.FinancialOutstanding.ShouldBe(60.50m); row.Collectible.ShouldBe(60.50m);
		await AssertConsumerDetailParityAsync(owner, row);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId)).Amount.ShouldBe(100.75m);
			(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(fixture.Source.Collection.CollectionId)).TotalPaidFee.ShouldBe(40.25m);
		});
	}

	[Fact]
	public async Task Canonical_Consumer_Position_Should_Mark_Missing_Evidence_For_Review_Instead_Of_Paid()
	{
		var first = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student);
		var owner = first.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var sibling = await SeedRefundSourceForSamePayerAsync(first);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>()
			.DeleteAsync(first.Collection.CollectionId, true));
		await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(first.Settlement.Posted.FinanceChargeId);
			var invalid = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(charge));
			invalid.Code.ShouldBe(LibraryFinanceErrorCodes.InvalidBalance);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory,
				owner.YearId, [owner.AssessmentId, sibling.Settlement.Owner.AssessmentId]));
			var execute = GetRequiredService<IAsyncQueryableExecuter>();
			(await execute.CountAsync(query)).ShouldBe(2);
			(await execute.CountAsync(query.Where(row => row.Evidence == FinanceExternalPositionEvidence.NeedsReview))).ShouldBe(1);
			(await execute.CountAsync(query.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified && row.Collectible > 0m))).ShouldBe(1);
		});
		var broken = await ConsumerPositionAsync(owner);
		broken.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		broken.SourceLinkId.ShouldNotBeNull(); broken.Collectible.ShouldBeNull(); broken.Settled.ShouldBeNull();
		broken.ReviewCode.ShouldNotBeNullOrWhiteSpace();
		await AssertConsumerDetailParityAsync(owner, broken);
		// Explicit corrupted-storage input: the bound version stays unchanged,
		// but a canonical date no longer matches accepted immutable source facts.
		// Both query and detail must validate the complete source, not just money.
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await repository.GetAsync(sibling.Settlement.Owner.AssessmentId);
			var version = source.GetAssessmentVersion();
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.SchoolDueDate))!
				.SetValue(source, source.SchoolDueDate!.Value.AddDays(1));
			await repository.UpdateAsync(source, true);
			source.GetAssessmentVersion().ShouldBe(version);
		});
		var wrongFacts = await ConsumerPositionAsync(sibling.Settlement.Owner);
		wrongFacts.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		wrongFacts.Collectible.ShouldBeNull();
		await AssertConsumerDetailParityAsync(sibling.Settlement.Owner, wrongFacts);
	}

	[Fact]
	public async Task Canonical_Consumer_Position_Should_Keep_Legacy_Paid_And_Waived_As_Unconfirmed_Declarations()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var ids = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var template = await repository.GetAsync(owner.AssessmentId);
			var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
			foreach (var (id, index) in ids.Select((id, index) => (id, index)))
			{
				var charge = new LibraryCharge(id, owner.PersonId, 1, template.ChargeCategoryId, null, 20.25m,
					new DateTime(2024, 6, 10), new DateTime(2024, 6, 24), owner.TenantId);
				// Historical imported state is seeded directly; forbidden Update is never used.
				typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(charge,
					index == 0 ? ChargePaymentStatus.Paid : ChargePaymentStatus.Waived);
				await repository.InsertAsync(charge, true);
			}
			return ids;
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = ConsumerPositionService(owner);
			var execute = GetRequiredService<IAsyncQueryableExecuter>();
			var query = await reader.GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory, AssessmentIds: ids));
			var rows = await execute.ToListAsync(query.OrderBy(row => row.SourceId));
			rows.Count.ShouldBe(2);
			rows.Select(row => row.RecordedSettlementDeclaration).ShouldBe(new[] {
				FinanceExternalRecordedSettlementDeclaration.LegacyPaid, FinanceExternalRecordedSettlementDeclaration.LegacyWaived }, ignoreOrder: true);
			foreach (var row in rows)
			{
				row.Evidence.ShouldBe(FinanceExternalPositionEvidence.Unposted); row.AssessmentOriginal.ShouldBe(20.25m);
				row.Currency.ShouldBeNull(); row.AcademicYearId.ShouldBeNull(); row.SourceLinkId.ShouldBeNull();
				row.Accepted.ShouldBeNull(); row.Settled.ShouldBeNull(); row.Collectible.ShouldBeNull();
			}
			var selected = await reader.GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory, owner.YearId, ids));
			(await execute.CountAsync(selected)).ShouldBe(0);
		});
		foreach (var id in ids)
		{
			var source = await ConsumerRawSourceAsync(id);
			var detail = (await WithUnitOfWorkAsync(() => GetRequiredService<IFinanceExternalPositionReader>().ReadAsync([source]))).Single();
			detail.Amounts.ShouldBeNull(); detail.Currency.ShouldBeNull();
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			foreach (var (id, index) in ids.Select((id, index) => (id, index)))
			{
				var stored = await repository.GetAsync(id);
				stored.Amount.ShouldBe(20.25m); stored.ChargeDate.ShouldBe(new DateTime(2024, 6, 10));
				stored.DueDate.ShouldBe(new DateTime(2024, 6, 24)); stored.SchoolChargeDate.ShouldBeNull();
				stored.SchoolDueDate.ShouldBeNull(); stored.AcademicYearId.ShouldBeNull(); stored.Currency.ShouldBeNull();
				stored.PaymentStatus.ShouldBe(index == 0 ? ChargePaymentStatus.Paid : ChargePaymentStatus.Waived);
			}
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(0);
		});
	}
}
