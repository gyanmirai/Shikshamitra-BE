using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<MemberChargeCopyConflict> SeedMemberChargeCopyConflictAsync()
	{
		MemberChargeCopySource? source = null;
		var settlement = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, prepareSource: async owner =>
		{
			source = await PrepareMemberChargeCopyConflictAsync(owner);
		});
		var retained = source ?? throw new InvalidOperationException("The conflicting legacy text must be present before Post.");
		var actor = settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(actor.TenantId);
		using var identity = SignIn(actor);
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == actor.TenantId && row.UserId == actor.ActorId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), actor.TenantId, userId: actor.ActorId, academicYearId: actor.YearId,
				educationBoardId: retained.Placement.BoardId, educationMediumId: retained.Placement.MediumId,
				educationMediumStageId: retained.Placement.StageId, description: "Actual recorded copy reader"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor.ActorId);
		});
		return new MemberChargeCopyConflict(actor, retained.LoanId, retained.BookId, retained.CopyId, retained.DecoyCopyId,
			retained.Title, retained.RecordedAccession);
	}

	private Task<MemberChargeCopySource> PrepareMemberChargeCopyConflictAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var placement = await CreateMovedCashPlacementAsync(owner, EducationBoardType.CBSE, "Member copy original");
		await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().InsertAsync(
			new(Guid.NewGuid(), owner.TenantId, owner.PersonId, placement.StageId), true);
		var category = new BookCategory("Member copy evidence", "Retained physical source", owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), owner.TenantId, owner.YearId, "Member copy evidence", 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		const string title = "Stable retained copy title";
		var book = new Book(title, "", "Library author", "", "", "", category.Id, Language.English, "", 0m, 0, 1, owner.YearId, owner.TenantId);
		var decoy = new Book("Unrelated accession title", "", "Library author", "", "", "", category.Id,
			Language.English, "", 0m, 0, 1, owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<Book, Guid>>().InsertManyAsync([book, decoy], true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, $"MC-A-{Guid.NewGuid():N}", section.Id, shelf.Id, owner.TenantId)
			{ Status = BookCopyStatus.Issued };
		var decoyCopy = new BookCopy(Guid.NewGuid(), decoy.Id, 1, $"MC-B-{Guid.NewGuid():N}", section.Id, shelf.Id, owner.TenantId);
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertManyAsync([copy, decoyCopy], true);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), owner.TenantId, copy.Id, decoyCopy.AccessionNumber, owner.PersonId, 1,
			new(2026, 11, 10), new(2026, 11, 24), new(owner.YearId, null, null, null, null, null,
				JsonSerializer.Serialize(new { Fixture = "Retained historical copy ID with conflicting original accession text" }),
				JsonSerializer.Serialize(new { Version = 1, Id = owner.PersonId, Role = 1, AcademicYearId = owner.YearId }),
				JsonSerializer.Serialize(new { SchoolIssueDate = "2026-11-10", SchoolDueDate = "2026-11-24" }),
				owner.ActorId, new DateTimeOffset(GetRequiredService<IClock>().Now.ToUniversalTime())));
		var capture = await GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
			loan.Id, owner.PersonId, owner.YearId, LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(owner.ActorId));
		capture.IsComplete.ShouldBeTrue(); capture.ExpectedAssignmentCount.ShouldBe(1);
		loan.BindStaffScopeCapture(capture);
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var assessment = await charges.GetAsync(owner.AssessmentId);
		assessment.FinanceSourceLinkId.ShouldBeNull(); assessment.MemberId.ShouldBe(owner.PersonId); assessment.MemberRole.ShouldBe(1);
		assessment.BookIssueId = loan.Id;
		await charges.UpdateAsync(assessment, true);
		return new MemberChargeCopySource(loan.Id, book.Id, copy.Id, decoyCopy.Id, title, decoyCopy.AccessionNumber, placement);
	});

	private sealed record MemberChargeCopySource(Guid LoanId, Guid BookId, Guid CopyId, Guid DecoyCopyId,
		string Title, string RecordedAccession, MovedCashPlacement Placement);
	private sealed record MemberChargeCopyConflict(Fixture Owner, Guid LoanId, Guid BookId, Guid CopyId,
		Guid DecoyCopyId, string Title, string RecordedAccession);
}

