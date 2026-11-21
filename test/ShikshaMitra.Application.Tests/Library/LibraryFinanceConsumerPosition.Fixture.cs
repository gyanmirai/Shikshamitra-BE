using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// The real DB checker is injected into the owning adapter, never AlwaysAllow.
	private LibraryChargePositionReader ConsumerPositionService(Fixture actor) =>
		ActivatorUtilities.CreateInstance<LibraryChargePositionReader>(
			new FinanceServices(ServiceProvider, actor, useStoredSource: true));

	private Task<FinanceExternalPositionQueryRow> ConsumerPositionAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var query = await ConsumerPositionService(owner).GetQueryAsync(new(
			LibraryChargePositionReadPurpose.ChargeHistory, owner.YearId, [owner.AssessmentId]));
		return await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query);
	});

	private Task<FinanceExternalPositionSource> ConsumerRawSourceAsync(Guid assessmentId) => WithUnitOfWorkAsync(async () =>
	{
		var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(assessmentId, includeDetails: false);
		var category = await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>()
			.FindAsync(charge.ChargeCategoryId, includeDetails: false);
		var aliases = await GetRequiredService<IRepository<LibraryMember, Guid>>().GetQueryableAsync();
		var student = charge.MemberRole == 0 ? await GetRequiredService<IAsyncQueryableExecuter>().FirstOrDefaultAsync(
			aliases.Where(row => row.TenantId == charge.TenantId && row.Id == charge.MemberId).Select(row => (Guid?)row.StudentId)) : null;
		return new FinanceExternalPositionSource
		{
			TenantId = charge.TenantId!.Value, Kind = FinanceExternalSourceKind.LibraryAssessment,
			SourceId = charge.Id, SourceVersion = charge.GetAssessmentVersion(), ExpectedSourceLinkId = charge.FinanceSourceLinkId,
			AcademicYearId = charge.AcademicYearId, AcademicYearConfirmed = charge.AcademicYearId.HasValue &&
				charge.AcademicYearProvenance != LibraryAcademicYearProvenance.Unconfirmed && Enum.IsDefined(charge.AcademicYearProvenance),
			AcademicYearEvidenceJson = charge.AcademicYearEvidenceJson,
			PayerKind = charge.MemberRole == 0 ? FinanceExternalPayerKind.Student : FinanceExternalPayerKind.Employee,
			StudentId = charge.MemberRole == 0 ? student ?? charge.MemberId : null,
			EmployeeId = charge.MemberRole is 1 or 2 ? charge.MemberId : null,
			AssessmentOriginal = charge.Amount, RecordedCurrency = charge.Currency,
			SchoolAssessmentDate = charge.SchoolChargeDate, SchoolDueDate = charge.SchoolDueDate,
			SchoolDatesConfirmed = charge.SchoolChargeDate.HasValue && charge.SchoolDueDate.HasValue &&
				(charge.ChargeDateProvenance is LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy) &&
				(charge.DueDateProvenance is LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy),
			SourceDateEvidenceJson = charge.SourceDateEvidenceJson,
			RawSourceCategoryId = charge.ChargeCategoryId, RawSourceRemarks = charge.Remarks,
			CurrentLegacyResolvedReason = string.IsNullOrWhiteSpace(charge.Remarks) ? category?.ChargeType : charge.Remarks,
			RecordedSettlementDeclaration = (FinanceExternalRecordedSettlementDeclaration)charge.PaymentStatus
		};
	});

	private async Task AssertConsumerDetailParityAsync(Fixture owner, FinanceExternalPositionQueryRow row)
	{
		var source = await ConsumerRawSourceAsync(owner.AssessmentId);
		var details = await WithUnitOfWorkAsync(() => GetRequiredService<IFinanceExternalPositionReader>().ReadAsync([source]));
		var detail = details.ShouldHaveSingleItem();
		detail.SourceId.ShouldBe(row.SourceId);
		detail.SourceLinkId.ShouldBe(row.SourceLinkId);
		detail.Evidence.ShouldBe(row.Evidence);
		detail.Currency.ShouldBe(row.Currency);
		if (row.Evidence != FinanceExternalPositionEvidence.Verified)
		{
			detail.Amounts.ShouldBeNull();
			if (row.Evidence == FinanceExternalPositionEvidence.NeedsReview) detail.ReviewIssues.ShouldNotBeEmpty();
			return;
		}
		detail.Amounts.ShouldNotBeNull();
		detail.Amounts.Original.ShouldBe(row.Original!.Value);
		detail.Amounts.Accepted.ShouldBe(row.Accepted!.Value);
		detail.Amounts.Settled.ShouldBe(row.Settled!.Value);
		detail.Amounts.Pending.ShouldBe(row.Pending!.Value);
		detail.Amounts.NetWaived.ShouldBe(row.NetWaived!.Value);
		detail.Amounts.FinancialOutstanding.ShouldBe(row.FinancialOutstanding!.Value);
		detail.Amounts.Collectible.ShouldBe(row.Collectible!.Value);
		detail.Amounts.Refunded.ShouldBe(row.Refunded!.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().GetAsync(row.SourceLinkId!.Value);
			var charge = await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(link.FinanceChargeId);
			var canonical = (await GetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(charge)).Amounts;
			row.Original.ShouldBe(canonical.Original); row.Accepted.ShouldBe(canonical.Accepted);
			row.Settled.ShouldBe(canonical.Settled); row.Pending.ShouldBe(canonical.PendingVerification);
			row.NetWaived.ShouldBe(canonical.NetWaived); row.Collectible.ShouldBe(canonical.Collectible);
			row.FinancialOutstanding.ShouldBe(canonical.Unsettled);
		});
	}

	private Task SetConsumerGrantAsync(Fixture owner, string action, bool granted) => WithUnitOfWorkAsync(() =>
		GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", granted));
}
