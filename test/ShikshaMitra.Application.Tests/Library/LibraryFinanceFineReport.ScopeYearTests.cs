using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Fine_Collection_Should_Use_Recorded_Source_And_Independent_Assessment_Years_Before_Rows(bool currentPlacement)
	{
		var moved = await SeedMovedTypedCashAsync();
		var owner = moved.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var saved = await SeedFineReportIndependentYearAsync(moved);
		var retained = await CaptureDashboardClockSourceAsync(owner);
		await SetMovedCashReaderScopeAsync(moved, currentPlacement);
		var denied = await RestrictFineReportReadAsync(owner);
		await AssertFineReportAuthorityAsync(owner, denied);
		await AssertFineReportMovedScopeAsync(moved, currentPlacement);
		var services = FineReportServices(owner);
		var inaccessible = FineReportRequest(owner);
		inaccessible.Filters.AcademicYearId = saved.YearId;
		var deniedYear = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(inaccessible)));
		deniedYear.Code.ShouldBe(LibraryErrorCodes.ReportScopeAccessDenied);
		var beforeQuery = await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Reports));
			return await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query.Select(row => row.SourceId));
		});
		beforeQuery.ShouldNotContain(saved.AssessmentId); beforeQuery.ShouldNotContain(saved.UnknownYearAssessmentId);
		beforeQuery.Contains(owner.AssessmentId).ShouldBe(!currentPlacement);

		var initial = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(FineReportRequest(owner)));

		initial.TotalCount.ShouldBe(currentPlacement ? 0 : 1);
		if (!currentPlacement)
		{
			var row = initial.Rows.ShouldHaveSingleItem();
			row["amount"].ShouldBe("100.75"); row["settledAmountText"].ShouldBe("40.25");
			row["memberName"].ShouldBe(saved.RecordedName); row["memberDisplaySource"].ShouldBe("LoanSnapshot");
			row.Keys.ShouldNotContain("email"); row.Keys.ShouldNotContain("phone");
		}
		await GrantFineReportIndependentYearAsync(owner, saved.YearId);
		await AssertFineReportMovedScopeAsync(moved, currentPlacement);
		await AssertFineReportAuthorityAsync(owner, denied);
		var queryIds = await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Reports));
			return await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query.Select(row => row.SourceId));
		});
		queryIds.Contains(saved.AssessmentId).ShouldBe(!currentPlacement);
		queryIds.ShouldNotContain(saved.UnknownYearAssessmentId);
		var later = FineReportRequest(owner);
		later.Filters.AcademicYearId = saved.YearId;
		later.Filters.FromDate = new DateTime(2027, 11, 17); later.Filters.ToDate = new DateTime(2027, 11, 17);
		var report = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(later));
		report.TotalCount.ShouldBe(currentPlacement ? 0 : 1);
		if (!currentPlacement)
		{
			var row = report.Rows.ShouldHaveSingleItem();
			row["amount"].ShouldBe("75"); row["financeEvidence"].ShouldBe("Unposted");
			row["academicYearId"].ShouldBe(saved.YearId.ToString());
			row["schoolChargeDate"].ShouldBe("2027-11-17"); row["chargeDate"].ShouldBeNullOrEmpty();
			foreach (var field in FineReportAmountFields) row[field].ShouldBeNull();
		}
		var allYears = FineReportRequest(owner); allYears.Filters.AcademicYearId = null;
		var all = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(allYears));
		all.TotalCount.ShouldBe(currentPlacement ? 0 : 2);
		all.Rows.ShouldNotContain(row => row["amount"] == "45.25");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}
}
