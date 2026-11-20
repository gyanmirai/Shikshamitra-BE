using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryLegacyCanonicalAdapter_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Route_Adapters_Should_Use_The_Same_Canonical_Loan_And_Replay_Without_Extra_Copy_Events()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = fixture.Catalog.CopyId, MemberId = fixture.StudentId, IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 24)
		});
		var issue = new IssueBookRequest { Checkout = new()
		{
			CopyId = plan.CopyId, MemberId = plan.MemberId, IssueDate = plan.IssueDate, RequestedDueDate = plan.DueDate,
			AcademicYearId = plan.AcademicYearId, Revision = plan.Revision, IdempotencyKey = "adapter-issue"
		} };
		var adapter = GetRequiredService<IBookIssueAppService>();
		var created = await adapter.IssueAsync(issue);
		(await adapter.IssueAsync(issue)).Id.ShouldBe(created.Id);
		var loanId = Guid.Parse(created.Id);
		var loans = GetRequiredService<ILibraryLoanAppService>();
		var loan = await loans.GetAsync(loanId);
		loan.AcademicYearId.ShouldBe(fixture.Catalog.YearId);
		loan.RawIssueDate.ShouldBeNull();
		loan.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 17));
		var returned = new ReturnBookRequest { LoanId = loanId, Return = new()
		{
			CopyId = plan.CopyId, Revision = loan.Revision, IdempotencyKey = "adapter-return",
			SchoolReturnDate = new(2026, 11, 18), CopyStatus = (int)BookCopyStatus.Available
		} };
		(await adapter.ReturnAsync(returned)).Id.ShouldBe(created.Id);
		(await adapter.ReturnAsync(returned)).Id.ShouldBe(created.Id);
		loan = await loans.GetAsync(loanId);
		loan.Status.ShouldBe((int)BookIssueStatus.Returned);
		loan.RawReturnDate.ShouldBeNull();
		loan.SchoolReturnDate.ShouldBe(new DateOnly(2026, 11, 18));
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().CountAsync(row => row.CopyId == plan.CopyId)).ShouldBe(2);
			(await GetRequiredService<IRepository<LibraryLoanCommand, Guid>>().CountAsync(row => row.LoanId == loanId)).ShouldBe(2);
		});
	}

	[Fact]
	public async Task A_Mixed_Contract_Should_Not_Allow_A_Conflicting_Accession_To_Be_Ignored()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookIssueAppService>().IssueAsync(new()
		{
			AccessionNo = "a different physical copy", Checkout = new()
			{
				CopyId = fixture.Catalog.CopyId, MemberId = fixture.StudentId, IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 24),
				Revision = "unaccepted", IdempotencyKey = "mixed-contract"
			}
		}));
		error.Code.ShouldBe(LibraryCirculationErrorCodes.CanonicalCommandRequired);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().CountAsync(row => row.BookCopyId == fixture.Catalog.CopyId)).ShouldBe(0));
	}
}
