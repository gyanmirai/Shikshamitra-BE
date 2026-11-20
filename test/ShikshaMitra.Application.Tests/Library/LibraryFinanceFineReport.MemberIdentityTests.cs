using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Fine_Collection_Should_Keep_The_Library_Card_Of_A_Direct_Student_Assessment()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Student, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		const string cardNumber = "LIB-REPORT-0061";
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var assessment = await charges.GetAsync(owner.AssessmentId);
			assessment.MemberId.ShouldBe(owner.PersonId);
			assessment.BookIssueId.ShouldBeNull();
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(owner.PersonId, includeDetails: false);
			student.AssignGrNo(6713);
			await students.UpdateAsync(student, true);
			await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(
				new LibraryMember(Guid.NewGuid(), owner.TenantId, owner.PersonId, cardNumber), true);
		});
		var denied = await RestrictFineReportReadAsync(owner);
		await AssertFineReportAuthorityAsync(owner, denied);
		(await FineReportPositionAsync(owner)).Evidence.ShouldBe(FinanceExternalPositionEvidence.Unposted);
		var request = FineReportRequest(owner);
		request.Filters.AcademicYearId = null;
		request.Columns = ["memberIdentifier", "memberDisplaySource", "financeEvidence"];
		var report = await WithUnitOfWorkAsync(() => FineReportServices(owner).Reports.PreviewAsync(request));
		report.TotalCount.ShouldBe(1);
		var row = report.Rows.ShouldHaveSingleItem();
		row["memberDisplaySource"].ShouldBe("CurrentAuthorizedRecord");
		row["financeEvidence"].ShouldBe("Unposted");
		row["memberIdentifier"].ShouldBe(cardNumber);
	}
}
