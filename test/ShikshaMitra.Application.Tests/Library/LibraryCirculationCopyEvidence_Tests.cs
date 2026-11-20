using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	[Fact]
	public async Task Issue_And_Physical_Return_Should_Record_Ordered_Copy_Events_With_The_Actual_Loan_And_Actor()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("EVIDENCE"))).Single();
		var issued = await IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 16), new(2026, 11, 24));
		await ReturnCanonicalAsync(Guid.Parse(issued.Id), copy.Id, context, (int)BookCopyStatus.Damaged, remarks: "Torn cover received at desk");

		await WithUnitOfWorkAsync(async () =>
		{
			var events = (await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().GetListAsync(x => x.CopyId == copy.Id))
				.OrderBy(x => x.Sequence).ToList();
			events.Count.ShouldBe(2);
			events.Select(x => x.EventType).ShouldBe([LibraryCopyEventType.Issued, LibraryCopyEventType.Returned]);
			events.Select(x => x.Sequence).ShouldBe([1L, 2L]);
			events.ShouldAllBe(x => x.RelatedLoanId == Guid.Parse(issued.Id) &&
				x.ActorId == GetRequiredService<ICurrentUser>().Id && x.BookId == copy.BookId);
			events[0].PreviousStatus.ShouldBe(BookCopyStatus.Available);
			events[0].NextStatus.ShouldBe(BookCopyStatus.Issued);
			events[1].PreviousStatus.ShouldBe(BookCopyStatus.Issued);
			events[1].NextStatus.ShouldBe(BookCopyStatus.Damaged);
			events[1].NextRemarks.ShouldBe("Torn cover received at desk");
		});
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Return_Should_Require_Reviewed_Reconciliation_When_The_Accession_Does_Not_Identify_The_Loans_Physical_Copy(bool copyExists)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var accession = UniqueAccession("UNLINKED");
		BookCopy? copy = copyExists ? (await CreateCopiesAsync(context, accession)).Single() : null;
		var loanId = Guid.NewGuid();
		var unresolvedCopyId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await _bookIssueRepository.InsertAsync(new BookIssue(loanId, unresolvedCopyId, accession,
				context.ActiveStudentId, 0, new DateTime(2026, 11, 16), new DateTime(2026, 11, 24),
				GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id), true);
			if (copy != null)
			{
				var stored = await _bookCopyRepository.GetAsync(copy.Id);
				stored.Status = BookCopyStatus.Issued;
				await _bookCopyRepository.UpdateAsync(stored, true);
			}
		});

		var error = await Should.ThrowAsync<BusinessException>(() => ReturnCanonicalAsync(loanId, copy?.Id ?? unresolvedCopyId, context));

		error.Code.ShouldBe(LibraryErrorCodes.CopyLoanReconciliationRequired);
		await WithUnitOfWorkAsync(async () =>
		{
			var retained = await _bookIssueRepository.GetAsync(loanId);
			retained.Status.ShouldBe(BookIssueStatus.Issued);
			retained.ReturnDate.ShouldBeNull();
			retained.SchoolReturnDate.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Issue_Should_Detect_An_Open_Loan_By_Stable_Copy_Id_Even_When_Its_Legacy_Accession_Differs()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("STABLE"))).Single();
		await WithUnitOfWorkAsync(async () => await _bookIssueRepository.InsertAsync(new BookIssue(Guid.NewGuid(), copy.Id,
			UniqueAccession("OLD"), context.ActiveStudentId, 0, new DateTime(2026, 11, 16), new DateTime(2026, 11, 24),
			GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id), true));

		var error = await Should.ThrowAsync<BusinessException>(() => IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24)));

		error.Code.ShouldBe(LibraryErrorCodes.DuplicateActiveIssue);
	}

	[Fact]
	public async Task Return_Lookup_Should_Not_Present_An_Accession_Only_Candidate_As_A_Confirmed_Loan()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("CANDIDATE"))).Single();
		await WithUnitOfWorkAsync(async () => await _bookIssueRepository.InsertAsync(new BookIssue(Guid.NewGuid(), Guid.NewGuid(),
			copy.AccessionNumber, context.ActiveStudentId, 0, new DateTime(2026, 11, 16), new DateTime(2026, 11, 24),
			GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id), true));

		var error = await Should.ThrowAsync<BusinessException>(() => _bookIssueAppService.SearchReturnBookAsync(copy.AccessionNumber));

		error.Code.ShouldBe(LibraryErrorCodes.CopyLoanReconciliationRequired);
	}
}
