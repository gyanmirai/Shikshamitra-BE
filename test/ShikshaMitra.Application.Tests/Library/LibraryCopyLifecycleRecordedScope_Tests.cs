using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task E_RecordedScope_Should_Govern_Active_Loan_Capabilities_And_Event_References_After_Reassignment(bool staff, bool originalScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id);
			stored.BookCopyId.ShouldBe(fixture.Context.Catalog.CopyId);
			if (staff)
			{
				var captureId = stored.BorrowerScopeCaptureId ?? throw new InvalidOperationException("The real staff checkout did not capture its footprint.");
				var children = await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().GetListAsync(row => row.CaptureId == captureId);
				children.Count.ShouldBe(2);
			}
			var events = await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().GetListAsync(row => row.CopyId == stored.BookCopyId);
			events.Single(row => row.EventType == LibraryCopyEventType.Issued).RelatedLoanId.ShouldBe(loan.Id);
		});
		await MoveStatisticsBorrowerAndActorAsync(fixture, staff, originalScope);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope)).ShouldBe(originalScope);
			(await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(scope)).Contains(memberId).ShouldBe(!originalScope);
		});
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var state = await lifecycle.GetStateAsync(fixture.Context.Catalog.BookId, fixture.Context.Catalog.CopyId);
		state.HasOpenLoan.ShouldBeTrue();
		state.RequiresLoanReconciliation.ShouldBeFalse();
		state.Status.ShouldBe((int)BookCopyStatus.Issued);

		state.ActiveLoanId.ShouldBe(originalScope ? loan.Id : null);
		state.CanReturn.ShouldBe(originalScope);
		state.CanEditNotes.ShouldBe(originalScope);
		state.CanChangeCondition.ShouldBe(originalScope);
		var history = await lifecycle.GetEventsAsync(state.BookId, state.CopyId);
		history.Items.Single(row => row.EventType == nameof(LibraryCopyEventType.Issued)).RelatedLoanId.ShouldBe(originalScope ? loan.Id : null);
		var command = new LibraryCopyConditionRequest { Status = BookCopyStatus.Lost, Reason = "Recorded borrower reported a missing copy.", Revision = state.Revision };
		if (originalScope) (await lifecycle.ChangeConditionAsync(state.BookId, state.CopyId, command)).Status.ShouldBe((int)BookCopyStatus.Lost);
		else (await Should.ThrowAsync<BusinessException>(() => lifecycle.ChangeConditionAsync(state.BookId, state.CopyId, command)))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id)).Status.ShouldBe(BookIssueStatus.Issued);
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(state.CopyId)).Status
				.ShouldBe(originalScope ? BookCopyStatus.Lost : BookCopyStatus.Issued);
		});
	}
}
