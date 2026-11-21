using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Member_List_Should_Show_Verified_Collectible_For_The_Recorded_Year_Alongside_Legacy_Declaration()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student, prepareSource: ActivateMemberListStudentAsync);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		fixture.Collection.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		await AddMemberAssessmentInAnotherYearAsync(owner);
		var denied = await RestrictFineReportReadAsync(owner);
		await AssertFineReportAuthorityAsync(owner, denied);
		var request = FineReportRequest(owner);
		request.ReportType = LibraryReportTypes.MemberList;
		var services = FineReportServices(owner);

		var report = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));
		var row = report.Rows.ShouldHaveSingleItem();
		row["pendingCharges"].ShouldBe("100.75");
		row["financeEvidence"].ShouldBe("Verified");
		row["currency"].ShouldBe("INR");
		row["collectibleAmountText"].ShouldBe("60.50");
		report.Columns.ShouldContain(column => column.Id == "collectibleAmountText" && column.Label == "Verified collectible");
		report.Columns.ShouldNotContain(column => column.Id == "pendingCharges");

		request.Columns = ["memberIdentifier", "pendingCharges", "financeEvidence", "currency", "collectibleAmountText"];
		var explicitColumns = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));
		explicitColumns.Columns.Select(column => column.Id).ShouldBe(request.Columns);
		explicitColumns.Columns.Single(column => column.Id == "pendingCharges").Label.ShouldBe("Recorded pending assessments");
		explicitColumns.Rows.Single()["collectibleAmountText"].ShouldBe("60.50");
	}

	[Fact]
	public async Task Member_List_Should_Represent_An_Unposted_Assessment_Without_An_Implied_Zero_Balance()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Student, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await ActivateMemberListStudentAsync(owner);
		await ConfirmStoredAssessmentAsync(owner);
		var denied = await RestrictFineReportReadAsync(owner);
		await AssertFineReportAuthorityAsync(owner, denied);
		var request = FineReportRequest(owner);
		request.ReportType = LibraryReportTypes.MemberList;
		var report = await WithUnitOfWorkAsync(() => FineReportServices(owner).Reports.PreviewAsync(request));

		var row = report.Rows.ShouldHaveSingleItem();
		row["financeEvidence"].ShouldBe("Unposted");
		row["collectibleAmountText"].ShouldBeNull();
		row["currency"].ShouldBeNull();
	}

	private Task ActivateMemberListStudentAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var student = await students.GetAsync(owner.PersonId);
		student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		student.AdditionalDetail!.IsLibraryMember = true;
		await students.UpdateAsync(student, true);
	});

	private Task AddMemberAssessmentInAnotherYearAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var previous = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Previous assessment year",
			new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(previous, true);
		var category = new LibraryChargeCategory(Guid.NewGuid(), owner.TenantId, previous.Id, "Previous Library assessment", 20m, false,
			"Used for the other-year isolation check.");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		var assessed = new DateOnly(2026, 2, 4);
		var due = assessed.AddDays(7);
		var id = Guid.NewGuid();
		await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(LibraryCharge.CreateConfirmed(id, owner.PersonId, 0,
			category.Id, null, 20m, assessed, due, previous.Id, LibraryAcademicYearProvenance.ExplicitAssessmentContext,
			JsonSerializer.Serialize(new { AcademicYearId = previous.Id, owner.ActorId }), JsonSerializer.Serialize(new { assessed, due, owner.ActorId }),
			"Unposted assessment recorded in a different academic year.", owner.TenantId), true);
	});
}
