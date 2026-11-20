using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryLegacyCommandBoundary_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Raw_Timestamp_Only_Checkout_Should_Require_An_Explicit_Reviewed_Canonical_Command()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var accession = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).AccessionNumber);
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookIssueAppService>().IssueAsync(new()
		{
			AccessionNo = accession, MemberId = fixture.StudentId,
			IssueDate = new(2026, 11, 17), DueDate = new(2026, 11, 24)
		}));
		error.Code.ShouldBe("Library:Circulation:010089");
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().CountAsync(row => row.BookCopyId == fixture.Catalog.CopyId)).ShouldBe(0);
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Available);
		});
	}

	[Fact]
	public async Task Accession_Only_Return_Should_Require_Exact_Loan_Copy_Revision_And_Explicit_School_Date()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		var accession = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).AccessionNumber);
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookIssueAppService>().ReturnAsync(new()
		{
			AccessionNo = accession, MemberId = fixture.StudentId, ReturnDate = new(2026, 11, 17)
		}));
		error.Code.ShouldBe("Library:Circulation:010089");
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(fixture.LoanId!.Value)).Status.ShouldBe(BookIssueStatus.Issued);
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Issued);
		});
	}
}
