using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCopyLifecycle_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Reporting_A_Lost_Loaned_Copy_Should_Keep_The_Loan_And_Existing_Assessment_Open()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);

		await LegacyStatusAsync(context, BookCopyStatus.Lost, "Borrower reported the copy missing");

		await WithUnitOfWorkAsync(async () =>
		{
			(await CopyAsync(context)).Status.ShouldBe(BookCopyStatus.Lost);
			var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(context.LoanId!.Value);
			loan.Status.ShouldBe(BookIssueStatus.Issued);
			loan.ReturnDate.ShouldBeNull();
			var charges = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetListAsync();
			charges.Single().Id.ShouldBe(context.ChargeId!.Value);
			charges.Single().PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charges.Single().Amount.ShouldBe(50m);
		});
	}

	[Fact]
	public async Task Lost_Copy_With_An_Open_Loan_Should_Offer_Explicit_Return_Instead_Of_Inferring_From_Condition()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await CopyAsync(context);
			copy.Status = BookCopyStatus.Lost;
			await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
		});

		var state = await Lifecycle().GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		state.HasOpenLoan.ShouldBeTrue();
		state.ActiveLoanId.ShouldBe(context.LoanId);
		state.CanReturn.ShouldBeTrue();
		state.CanEditNotes.ShouldBeTrue();
		state.CanMove.ShouldBeFalse();
		state.CanWriteOff.ShouldBeFalse();
		state.AllowedConditionStatuses.ShouldNotContain((int)BookCopyStatus.Available);
		await Should.ThrowAsync<BusinessException>(() => Lifecycle().WriteOffAsync(state.BookId, state.CopyId,
			new() { Reason = "Proposed write-off", Revision = state.Revision }));
		await Should.ThrowAsync<BusinessException>(() => Lifecycle().MoveAsync(state.BookId, state.CopyId,
			new() { SectionId = state.SectionId, ShelfId = context.OtherShelfId, Reason = "Proposed move", Revision = state.Revision }));
	}

	[Fact]
	public async Task Found_Copy_With_An_Open_Loan_Should_Require_The_Return_Workflow()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await CopyAsync(context);
			copy.Status = BookCopyStatus.Lost;
			await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
		});

		await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Available, "Found on campus"));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(context.LoanId!.Value)).Status.ShouldBe(BookIssueStatus.Issued));
	}

	[Fact]
	public async Task Under_Repair_Copy_Should_Not_Be_Issued()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await LegacyStatusAsync(context, BookCopyStatus.UnderRepair, "Binding repair in progress");
		var accession = await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).AccessionNumber);

		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookIssueAppService>().IssueAsync(new()
		{
			AccessionNo = accession, MemberId = context.StudentId,
			IssueDate = new(2026, 11, 16), DueDate = new(2026, 11, 23)
		}));
	}

	[Fact]
	public async Task Write_Off_Should_Require_A_Reason_Through_The_Legacy_Status_Endpoint()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();

		await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Discarded, null));
	}

	[Fact]
	public async Task A_Class_Scoped_Catalogue_Editor_Should_Not_Write_Off_Tenant_Inventory()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await RestrictScopeAsync(context);

		var error = await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Discarded, "Remove from service"));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Theory]
	[InlineData(BookCopyStatus.Reserved)]
	[InlineData((BookCopyStatus)999)]
	public async Task Manual_Condition_Edit_Should_Reject_Workflow_Only_Or_Unknown_Statuses(BookCopyStatus target)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();

		await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, target, "Manual status edit"));
	}

	[Fact]
	public async Task Persisted_Copy_Location_Edit_Should_Require_A_Reasoned_Move_Command()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var copy = await WithUnitOfWorkAsync(() => CopyAsync(context));
		var catalogue = GetRequiredService<IBookAppService>();
		var opened = await catalogue.GetAsync(copy.BookId);

		var error = await Should.ThrowAsync<BusinessException>(() => catalogue
			.PutCopiesAsync(copy.BookId, new()
			{
				ExpectedCopiesRevision = opened.CopiesRevision,
				Copies = [new() { CopyId = copy.Id, CopyNumber = copy.CopyNumber, AccessionNumber = copy.AccessionNumber,
					SectionId = copy.SectionId, ShelfId = context.OtherShelfId }]
			}));

		error.Message.ShouldContain("move", Case.Insensitive);
		await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).ShelfId.ShouldBe(context.Catalog.ShelfId));
	}

	[Fact]
	public async Task Reserved_Copy_Should_Require_Releasing_The_Hold_Before_A_Manual_Condition_Change()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await CopyAsync(context);
			copy.Status = BookCopyStatus.Reserved;
			await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
		});

		await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Damaged, "Cover torn"));
	}

	[Fact]
	public async Task Copy_State_And_Events_Should_Not_Be_Read_From_Another_Tenant()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var lifecycle = Lifecycle();
		using var foreignSchool = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());

		await Should.ThrowAsync<EntityNotFoundException>(() => lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId));
		await Should.ThrowAsync<EntityNotFoundException>(() => lifecycle.GetEventsAsync(context.Catalog.BookId, context.Catalog.CopyId));
	}

	[Fact]
	public async Task Physical_State_And_Event_Readers_Should_Redact_An_Inaccessible_Borrowers_Loan_Id()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		await LegacyStatusAsync(context, BookCopyStatus.Lost, "Borrower reported missing");
		await RestrictScopeAsync(context);
		var lifecycle = Lifecycle();

		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);
		var events = await lifecycle.GetEventsAsync(context.Catalog.BookId, context.Catalog.CopyId);

		state.HasOpenLoan.ShouldBeTrue();
		state.ActiveLoanId.ShouldBeNull();
		state.CanReturn.ShouldBeFalse();
		state.CanChangeCondition.ShouldBeFalse();
		state.CanEditNotes.ShouldBeFalse();
		state.CanWriteOff.ShouldBeFalse();
		events.Items.ShouldNotBeEmpty();
		events.Items.ShouldAllBe(row => row.RelatedLoanId == null);
	}

	[Fact]
	public async Task Same_Status_Remarks_Changes_Should_Preserve_The_Previous_And_New_Evidence()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await LegacyStatusAsync(context, BookCopyStatus.Available, "Initial shelf inspection");
		await LegacyStatusAsync(context, BookCopyStatus.Available, "Corrected shelf inspection");

		var events = await Lifecycle().GetEventsAsync(context.Catalog.BookId, context.Catalog.CopyId);

		events.Items.ShouldContain(row => row.EventType == "NotesChanged" &&
			row.PreviousRemarks == "Initial shelf inspection" && row.NextRemarks == "Corrected shelf inspection");
	}

	[Fact]
	public async Task Movement_Should_Keep_An_Immutable_Actor_Reason_And_Location_Snapshot()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var lifecycle = Lifecycle();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);
		await lifecycle.MoveAsync(state.BookId, state.CopyId, new()
		{
			SectionId = state.SectionId, ShelfId = context.OtherShelfId, Reason = "Reshelved after stock check", Revision = state.Revision
		});
		await LegacyStatusAsync(context, BookCopyStatus.Available, "Shelf verified");

		var events = await lifecycle.GetEventsAsync(state.BookId, state.CopyId);
		var movement = events.Items.Single(row => row.EventType == "Moved");
		movement.ActorId.ShouldBe(GetRequiredService<ICurrentUser>().Id!.Value);
		movement.Reason.ShouldBe("Reshelved after stock check");
		movement.FromShelfId.ShouldBe(context.Catalog.ShelfId);
		movement.ToShelfId.ShouldBe(context.OtherShelfId);
		movement.PreviousStatus.ShouldBe((int)BookCopyStatus.Available);
		movement.NextStatus.ShouldBe((int)BookCopyStatus.Available);
		movement.PreviousRemarks.ShouldBeNull();
		movement.RelatedLoanId.ShouldBeNull();
	}

	[Fact]
	public async Task A_Stale_Revision_Should_Not_Overwrite_A_Condition_Change_Or_Record_A_Move()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var lifecycle = Lifecycle();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);
		await lifecycle.ChangeConditionAsync(state.BookId, state.CopyId, new()
		{
			Status = BookCopyStatus.Damaged, Reason = "Cover torn", Revision = state.Revision
		});

		await Should.ThrowAsync<BusinessException>(() => lifecycle.MoveAsync(state.BookId, state.CopyId, new()
		{
			SectionId = state.SectionId, ShelfId = context.OtherShelfId, Reason = "Reshelve", Revision = state.Revision
		}));
		(await lifecycle.GetEventsAsync(state.BookId, state.CopyId)).TotalCount.ShouldBe(1);
		await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).ShelfId.ShouldBe(context.Catalog.ShelfId));
	}

	[Fact]
	public async Task Copies_With_Condition_Events_Should_Not_Be_Deleted_After_They_Become_Available_Again()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await LegacyStatusAsync(context, BookCopyStatus.Damaged, "Cover torn");
		await LegacyStatusAsync(context, BookCopyStatus.Available, "Cover repaired");
		var catalogue = GetRequiredService<IBookAppService>();
		var opened = await catalogue.GetAsync(context.Catalog.BookId);

		var error = await Should.ThrowAsync<BusinessException>(() => catalogue
			.PutCopiesAsync(context.Catalog.BookId, new() { ExpectedCopiesRevision = opened.CopiesRevision, Copies = [] }));
		error.Code.ShouldBe(LibraryErrorCodes.CopyHasHistory);
		await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).ShouldNotBeNull());
	}

	[Fact]
	public async Task Legacy_Stale_Form_Without_Revision_Should_Not_Overwrite_A_Newer_Condition()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var lifecycle = Lifecycle();
		var original = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);
		await lifecycle.ChangeConditionAsync(original.BookId, original.CopyId, new()
		{
			Status = BookCopyStatus.Damaged, Reason = "Cover torn during inspection", Revision = original.Revision
		});

		var error = await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context,
			BookCopyStatus.Available, "Old catalogue form saved after inspection", includeRevision: false));

		error.Code.ShouldBe("Library:Copies:010061");
		await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).Status.ShouldBe(BookCopyStatus.Damaged));
		(await lifecycle.GetEventsAsync(original.BookId, original.CopyId)).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Unloaned_Copy_Reads_And_Updates_Should_Require_Actual_Data_Scope_Assignments()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var userId = GetRequiredService<ICurrentUser>().Id!.Value;
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().DeleteAsync(row => row.UserId == userId, true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		});
		var lifecycle = Lifecycle();

		var stateError = await Should.ThrowAsync<BusinessException>(() => lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId));
		var eventsError = await Should.ThrowAsync<BusinessException>(() => lifecycle.GetEventsAsync(context.Catalog.BookId, context.Catalog.CopyId));
		var updateError = await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Lost, "No data scope assigned", includeRevision: false));

		stateError.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		eventsError.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		updateError.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).Status.ShouldBe(BookCopyStatus.Available));
	}

	[Fact]
	public async Task Loan_Linked_Condition_Changes_Should_Require_The_Borrowers_Data_Scope()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		await RestrictScopeAsync(context);

		var error = await Should.ThrowAsync<BusinessException>(() => LegacyStatusAsync(context, BookCopyStatus.Lost, "Reported missing"));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Accession_Only_Loan_Candidate_Should_Require_Reviewed_Reconciliation_Before_Any_Copy_Command()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		var referencedCopyId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(new BookCopy(referencedCopyId,
				context.Catalog.BookId, 2, $"OLD-{Guid.NewGuid():N}", context.Catalog.SectionId, context.Catalog.ShelfId,
				GetRequiredService<ICurrentTenant>().Id), true);
			var loans = GetRequiredService<IRepository<BookIssue, Guid>>();
			var loan = await loans.GetAsync(context.LoanId!.Value);
			loan.BookCopyId = referencedCopyId;
			await loans.UpdateAsync(loan, true);
		});
		var lifecycle = Lifecycle();

		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		state.RequiresLoanReconciliation.ShouldBeTrue();
		state.LoanLinkSource.ShouldBe("LegacyAccessionCandidate");
		state.HasOpenLoan.ShouldBeFalse();
		state.ActiveLoanId.ShouldBeNull();
		state.CanReturn.ShouldBeFalse();
		state.CanChangeCondition.ShouldBeFalse();
		state.CanMove.ShouldBeFalse();
		state.CanWriteOff.ShouldBeFalse();
		var error = await Should.ThrowAsync<BusinessException>(() => lifecycle.ChangeConditionAsync(state.BookId, state.CopyId,
			new() { Status = BookCopyStatus.Lost, Reason = "Reported missing", Revision = state.Revision }));
		error.Message.ShouldContain("reconcil", Case.Insensitive);
		(await lifecycle.GetEventsAsync(state.BookId, state.CopyId)).TotalCount.ShouldBe(0);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(context.LoanId!.Value)).BookCopyId.ShouldBe(referencedCopyId));
	}

	[Fact]
	public async Task Write_Off_Of_A_Returned_Copy_Should_Preserve_The_Copy_Loan_History_And_Pending_Finance()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var loans = GetRequiredService<IRepository<BookIssue, Guid>>();
			var loan = await loans.GetAsync(context.LoanId!.Value);
			loan.Status = BookIssueStatus.Returned;
			loan.ReturnDate = new(2026, 11, 16);
			await loans.UpdateAsync(loan, true);
			var copy = await CopyAsync(context);
			copy.Status = BookCopyStatus.Damaged;
			await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
		});
		var lifecycle = Lifecycle();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		var retired = await lifecycle.WriteOffAsync(state.BookId, state.CopyId, new()
		{
			Reason = "Beyond repair following inspection", Revision = state.Revision
		});

		retired.Status.ShouldBe((int)BookCopyStatus.Discarded);
		await WithUnitOfWorkAsync(async () =>
		{
			(await CopyAsync(context)).Id.ShouldBe(context.Catalog.CopyId);
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(context.LoanId!.Value)).BookCopyId.ShouldBe(context.Catalog.CopyId);
			var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.ChargeId!.Value);
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charge.PaidDate.ShouldBeNull();
		});
	}

	private ILibraryCopyLifecycleAppService Lifecycle()
	{
		var service = ServiceProvider.GetService<ILibraryCopyLifecycleAppService>();
		service.ShouldNotBeNull("Copy condition commands must retain immutable physical history independently of loans and Finance.");
		return service;
	}

	private Task<LibraryCopyLifecycleTestContext> SeedAsync(bool onLoan = false) =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan));

	private Task<BookCopy> CopyAsync(LibraryCopyLifecycleTestContext context) =>
		GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);

	private async Task LegacyStatusAsync(LibraryCopyLifecycleTestContext context, BookCopyStatus status, string? remarks, bool includeRevision = true)
	{
		var accession = await WithUnitOfWorkAsync(async () => (await CopyAsync(context)).AccessionNumber);
		var state = includeRevision ? await Lifecycle().GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId) : null;
		await GetRequiredService<IBookAppService>().UpdateCopyStatusAsync(context.Catalog.BookId, accession,
			new() { Status = status, Remarks = remarks, Revision = state?.Revision });
	}

	private Task RestrictScopeAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == userId, true);
		await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
			userId: userId, academicYearId: context.Catalog.YearId, educationBoardId: context.BoardId,
			educationMediumId: context.MediumId, educationMediumStageId: context.StageId, standardId: context.OtherClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
	});
}
