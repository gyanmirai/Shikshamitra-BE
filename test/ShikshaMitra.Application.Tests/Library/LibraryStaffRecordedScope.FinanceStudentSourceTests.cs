using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task A_Loan_Linked_Student_Assessment_Should_Retain_Recorded_Class_Authority_Through_A_Legacy_Member_Alias()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedMovedStudentFinanceSourceAsync();
		await SetFinanceStudentClassAsync(context.Placement, context.Placement.BorrowerClassId);
		await AssertFinanceStudentScopeAsync(context, recorded: true);
		var reader = await ActualFinanceSourceReaderAsync();

		var facts = await WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(context.Source));

		facts.Payer.StudentId.ShouldBe(context.Placement.StudentId);
		facts.Payer.EmployeeId.ShouldBeNull();
		facts.Payer.Kind.ShouldBe(FinanceExternalPayerKind.Student);
		facts.Source.SourceId.ShouldBe(context.Source.SourceId);
		facts.AcademicYearId.ShouldBe(context.Placement.Catalog.YearId);
	}

	[Fact]
	public async Task A_Students_Current_Class_Should_Not_Grant_Their_Old_Loan_Linked_Assessment_Through_A_Legacy_Alias()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedMovedStudentFinanceSourceAsync();
		await SetFinanceStudentClassAsync(context.Placement, context.Placement.OtherClassId);
		await AssertFinanceStudentScopeAsync(context, recorded: false);
		var reader = await ActualFinanceSourceReaderAsync();

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(context.Source)));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	private Task<FinanceStudentSource> SeedMovedStudentFinanceSourceAsync() => WithUnitOfWorkAsync(async () =>
	{
		var placement = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
		var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var membership = new LibraryMember(Guid.NewGuid(), tenant, placement.StudentId, $"HIST-{Guid.NewGuid():N}", false);
		await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(membership, true);
		var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(placement.Catalog.CopyId);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, copy.Id, copy.AccessionNumber, placement.StudentId, 0,
			new(2026, 11, 1), new(2026, 11, 8), new(placement.Catalog.YearId, placement.BoardId, placement.MediumId,
				placement.StageId, placement.BorrowerClassId, null, "{}", "{}", "{}", actor,
				new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero)));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		var category = new LibraryChargeCategory(Guid.NewGuid(), tenant, placement.Catalog.YearId, "Recorded student assessment", 75m, false,
			"Preserved original class source");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		var charge = LibraryCharge.CreateConfirmed(Guid.NewGuid(), membership.Id, 0, category.Id, loan.Id, 75m,
			new(2026, 11, 17), new(2026, 11, 30), placement.Catalog.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
			JsonSerializer.Serialize(new { LoanId = loan.Id, OriginalAliasId = membership.Id, CanonicalStudentId = placement.StudentId,
				AcademicYearId = placement.Catalog.YearId, ActorId = actor, Reason = "Reviewed original student loan and assessment register." }),
			JsonSerializer.Serialize(new { ActorId = actor, Reason = "Reviewed original assessment school dates." }),
			"Reviewed recovery assessment preserving the original student member alias.", tenant);
		await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var student = await students.GetAsync(placement.StudentId);
		student.UpdateAcademicPlacement(placement.Catalog.YearId, placement.BoardId, placement.MediumId, placement.StageId,
			placement.OtherClassId, null);
		await students.UpdateAsync(student, true);
		return new FinanceStudentSource(placement, loan.Id, membership.Id, new()
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = charge.Id, SourceVersion = charge.GetAssessmentVersion()
		});
	});

	private Task SetFinanceStudentClassAsync(LibraryCopyLifecycleTestContext placement, Guid classId) => WithUnitOfWorkAsync(async () =>
	{
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == actor, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
			academicYearId: placement.Catalog.YearId, educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
			educationMediumStageId: placement.StageId, standardId: classId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	});

	private async Task AssertFinanceStudentScopeAsync(FinanceStudentSource context, bool recorded)
	{
		context.MemberAliasId.ShouldNotBe(context.Placement.StudentId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(context.LoanId, scope))).ShouldBe(recorded);
		(await WithUnitOfWorkAsync(async () => await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(
			(await GetRequiredService<ILibraryAccessService>().GetStudentsAsync()).Where(row => row.Id == context.Placement.StudentId)))).ShouldBe(!recorded);
	}

	private sealed record FinanceStudentSource(LibraryCopyLifecycleTestContext Placement, Guid LoanId, Guid MemberAliasId,
		FinanceExternalSourceReference Source);
}
