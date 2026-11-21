using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<MemberChargeMovedSource> SeedMemberChargeMovedInSourceAsync(FinanceExternalPayerKind kind)
	{
		if (kind == FinanceExternalPayerKind.Employee)
		{
			var moved = await SeedMovedTypedCashAsync();
			var owner = moved.Settlement.Owner;
			using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
			using var actor = SignIn(owner);
			await SetMovedCashReaderScopeAsync(moved, currentPlacement: true);
			return new MemberChargeMovedSource(owner, moved.Source.LoanId, owner.PersonId);
		}
		kind.ShouldBe(FinanceExternalPayerKind.Student);
		MemberChargeStudentSource? recorded = null;
		var settlement = await SeedTypedSettlementAsync(kind, prepareSource: async owner =>
		{
			recorded = await PrepareMemberChargeStudentSourceAsync(owner);
		});
		var source = recorded ?? throw new InvalidOperationException("The actual Student loan must be captured before posting.");
		using var school = GetRequiredService<ICurrentTenant>().Change(settlement.Owner.TenantId);
		using var identity = SignIn(settlement.Owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == settlement.Owner.TenantId && row.UserId == settlement.Owner.ActorId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), settlement.Owner.TenantId, userId: settlement.Owner.ActorId,
				academicYearId: settlement.Owner.YearId, educationBoardId: source.Current.BoardId,
				educationMediumId: source.Current.MediumId, educationMediumStageId: source.Current.StageId,
				standardId: source.CurrentClassId, description: "Current actual member profile reader"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(settlement.Owner.ActorId);
		});
		return new MemberChargeMovedSource(settlement.Owner, source.LoanId, source.AliasId);
	}

	// Mirrors the existing FinanceStudentSource fixture using named factories.
	// Private helpers in LibraryStaffRecordedScope_Tests are not callable from
	// this partial; the actual current Finance seed supplies identity/payer/year.
	private Task<MemberChargeStudentSource> PrepareMemberChargeStudentSourceAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var original = await CreateMovedCashPlacementAsync(owner, EducationBoardType.CBSE, "Member original source");
		var current = await CreateMovedCashPlacementAsync(owner, EducationBoardType.GSEB, "Member current profile");
		original.BoardId.ShouldNotBe(current.BoardId); original.MediumId.ShouldNotBe(current.MediumId);
		var oldClass = new Standard(Guid.NewGuid(), owner.TenantId, "4", 9, original.StageId, null);
		var newClass = new Standard(Guid.NewGuid(), owner.TenantId, "5", 10, current.StageId, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertManyAsync([oldClass, newClass], true);
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var student = await students.GetAsync(owner.PersonId, includeDetails: false);
		student.UpdateAcademicPlacement(owner.YearId, original.BoardId, original.MediumId, original.StageId, oldClass.Id, null);
		await students.UpdateAsync(student, true);
		var alias = new LibraryMember(Guid.NewGuid(), owner.TenantId, owner.PersonId, $"MEM-HIST-{Guid.NewGuid():N}", false);
		await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(alias, true);
		var category = new BookCategory("Member recorded source", "Recorded Student scope", owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), owner.TenantId, owner.YearId, "Recorded member loan", 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		var book = new Book("Member recorded Student obligation", "", "Library author", "", "", "", category.Id,
			Language.English, "", 0m, 0, 1, owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, $"MC-{Guid.NewGuid():N}", section.Id, shelf.Id, owner.TenantId)
			{ Status = BookCopyStatus.Issued };
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), owner.TenantId, copy.Id, copy.AccessionNumber, owner.PersonId, 0,
			new(2026, 11, 10), new(2026, 11, 24), new(owner.YearId, original.BoardId, original.MediumId, original.StageId,
				oldClass.Id, null, JsonSerializer.Serialize(new { Fixture = "Named reviewed source loan" }),
				JsonSerializer.Serialize(new { Version = 1, Id = student.Id,
					MemberName = string.Join(" ", new[] { student.FirstName, student.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
					MemberIdentifier = alias.MemberIdentifier, Role = 0, AcademicYearId = owner.YearId }),
				JsonSerializer.Serialize(new { SchoolIssueDate = "2026-11-10", SchoolDueDate = "2026-11-24" }),
				owner.ActorId, new DateTimeOffset(GetRequiredService<IClock>().Now.ToUniversalTime())));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var assessment = await charges.GetAsync(owner.AssessmentId);
		assessment.FinanceSourceLinkId.ShouldBeNull(); assessment.MemberRole.ShouldBe(0);
		assessment.MemberId = alias.Id; assessment.BookIssueId = loan.Id;
		await charges.UpdateAsync(assessment, true);
		student.UpdateAcademicPlacement(owner.YearId, current.BoardId, current.MediumId, current.StageId, newClass.Id, null);
		await students.UpdateAsync(student, true);
		return new MemberChargeStudentSource(loan.Id, alias.Id, current, newClass.Id);
	});

	private Task AssertMovedInMemberChargePreconditionsAsync(MemberChargeMovedSource moved) => WithUnitOfWorkAsync(async () =>
	{
		var owner = moved.Owner;
		var access = GetRequiredService<ILibraryAccessService>();
		var context = await access.GetContextAsync(owner.YearId);
		context.UserScope.HasTenantWideScope().ShouldBeFalse();
		context.AllowedAcademicYearIds.ShouldContain(owner.YearId);
		(await access.EnsureMemberAsync(owner.PersonId)).ShouldBe(owner.PersonId);
		(await access.EnsureMemberAsync(moved.RecordedMemberId)).ShouldBe(owner.PersonId);
		(await access.GetPersonMemberIdsAsync(owner.PersonId)).ShouldContain(moved.RecordedMemberId);
		(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(moved.LoanId, context.UserScope)).ShouldBeFalse();
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
		source.TenantId.ShouldBe(owner.TenantId); source.AcademicYearId.ShouldBe(owner.YearId);
		source.MemberId.ShouldBe(moved.RecordedMemberId); source.BookIssueId.ShouldBe(moved.LoanId);
		source.FinanceSourceLinkId.ShouldNotBeNull();
		var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(moved.LoanId);
		loan.TenantId.ShouldBe(owner.TenantId); loan.AcademicYearId.ShouldBe(owner.YearId);
		loan.MemberId.ShouldBe(owner.PersonId);
		loan.MemberRole.ShouldBe(owner.PayerKind == FinanceExternalPayerKind.Student ? 0 : 1);
	});

	private sealed record MemberChargeMovedSource(Fixture Owner, Guid LoanId, Guid RecordedMemberId);
	private sealed record MemberChargeStudentSource(Guid LoanId, Guid AliasId, MovedCashPlacement Current, Guid CurrentClassId);
}

