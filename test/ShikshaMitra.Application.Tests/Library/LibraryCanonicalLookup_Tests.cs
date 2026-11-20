using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCanonicalLookup_Tests : ShikshaMitraApplicationTestBase<LibraryCanonicalLookupTestModule>
{
	[Fact]
	public async Task Copy_Lookup_Should_Return_The_Actual_Stable_Copy_Identity_For_Normalized_Accession_And_Unicode_Scan_Token()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var accession = await WithUnitOfWorkAsync(async () => (await Copies.GetAsync(context.Catalog.CopyId)).AccessionNumber);

		var first = await Lookups.GetCopyAsync($"  {accession.ToLowerInvariant()}  ");

		first.CopyId.ShouldBe(context.Catalog.CopyId);
		first.BookId.ShouldBe(context.Catalog.BookId);
		first.AccessionNo.ShouldBe(accession);
		var unicode = $"પુસ્તક/26 #A+B-{Guid.NewGuid():N}";
		var token = await WithUnitOfWorkAsync(async () =>
		{
			var copy = await Copies.GetAsync(context.Catalog.CopyId);
			copy.AccessionNumber = unicode;
			await Copies.UpdateAsync(copy, true);
			return LibraryCopyScanCode.For(copy);
		});
		var scanned = await Lookups.GetCopyAsync(token);
		scanned.CopyId.ShouldBe(first.CopyId);
		scanned.AccessionNo.ShouldBe(unicode);
	}

	[Fact]
	public async Task Return_Lookup_Should_Return_The_Exact_Open_Loan_And_Current_Canonical_Revision_Without_The_Student_Graph()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await SeedLoanAsync(context);
		var canonical = await Loans.GetAsync(loan.LoanId);
		using var activity = new Activity("Canonical return lookup SQL").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);

		var result = await Lookups.GetReturnAsync(loan.Accession);

		result.Copy.CopyId.ShouldBe(context.Catalog.CopyId);
		result.Loan.Id.ShouldBe(loan.LoanId);
		result.Loan.Revision.ShouldBe(canonical.Revision);
		result.Loan.CanReturn.ShouldBeTrue();
		result.Loan.RawIssueDate.ShouldBeNull();
		result.Borrower.MemberName.ShouldBe("Borrower at checkout");
		result.Borrower.DisplaySource.ShouldBe("LoanSnapshot");
		var studentSql = commands.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
		studentSql.ShouldNotBeEmpty("The test must observe the actual minimal borrower facts query.");
		foreach (var sql in studentSql)
		{
			sql.ShouldNotContain("StudentFamilyDetails");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppStudentDocuments");
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Return_Lookup_Should_Use_Stored_Loan_Scope_Rather_Than_The_Borrowers_Current_Year(bool historicScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await SeedLoanAsync(context, historic: true);
		await RestrictAsync(context, loan, historicScope);
		var currentMembers = await WithUnitOfWorkAsync(() => GetRequiredService<ShikshaMitra.Library.Shared.ILibraryAccessService>().GetMemberIdsAsync());
		if (!historicScope)
		{
			currentMembers.ShouldContain(context.StudentId);
			await Should.ThrowAsync<BusinessException>(() => Lookups.GetReturnAsync(loan.Accession));
			return;
		}
		currentMembers.ShouldNotContain(context.StudentId);

		var result = await Lookups.GetReturnAsync(loan.Accession);

		result.Loan.Id.ShouldBe(loan.LoanId);
		result.Loan.AcademicYearId.ShouldBe(context.Catalog.OriginYearId);
		result.Loan.CanReturn.ShouldBeTrue();
		result.Borrower.MemberName.ShouldBe("Borrower at checkout");
		result.Borrower.DisplaySource.ShouldBe("LoanSnapshot");
	}

	[Fact]
	public async Task Return_Lookup_Should_Not_Auto_Bind_An_Accession_Only_Loan_Candidate_To_The_Scanned_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await SeedLoanAsync(context);
		var otherId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await Copies.InsertAsync(new(otherId, context.Catalog.BookId, 2, $"OTHER-{Guid.NewGuid():N}", context.Catalog.SectionId, context.Catalog.ShelfId,
				GetRequiredService<ICurrentTenant>().Id), true);
			var stored = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.LoanId);
			stored.BookCopyId = otherId;
			await GetRequiredService<IRepository<BookIssue, Guid>>().UpdateAsync(stored, true);
		});

		var error = await Should.ThrowAsync<BusinessException>(() => Lookups.GetReturnAsync(loan.Accession));

		error.Code.ShouldBe(LibraryErrorCodes.CopyLoanReconciliationRequired);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.LoanId)).BookCopyId.ShouldBe(otherId);
			(await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().CountAsync()).ShouldBe(0);
		});
	}

	[Theory]
	[InlineData("Malformed")]
	[InlineData("WrongPerson")]
	[InlineData("WrongRole")]
	[InlineData("UnknownVersion")]
	public async Task Invalid_Borrower_Snapshot_Should_Not_Reveal_Current_Out_Of_Scope_Labels(string corruption)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await SeedLoanAsync(context, historic: true, corruption: corruption);
		await RestrictAsync(context, loan, historic: true);

		var result = await Lookups.GetReturnAsync(loan.Accession);

		result.Loan.Id.ShouldBe(loan.LoanId);
		result.Borrower.MemberName.ShouldBeNull();
		result.Borrower.MemberIdentifier.ShouldBeNull();
		result.Borrower.ClassDivision.ShouldBeNull();
		result.Borrower.DisplaySource.ShouldBe("Unavailable");
	}

	[Fact]
	public async Task Copy_Lookup_Should_Reject_Foreign_Tokens_And_A_Same_Tenant_Copy_With_A_Foreign_Parent()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		LibraryCopyLifecycleTestContext foreign;
		string foreignToken;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			foreign = await SeedAsync();
			foreignToken = $"SM-LIB-COPY:{foreign.Catalog.CopyId:N}";
		}
		var error = await Record.ExceptionAsync(() => Lookups.GetCopyAsync(foreignToken));
		(error is BusinessException or EntityNotFoundException).ShouldBeTrue();
		var accession = $"BAD-PARENT-{Guid.NewGuid():N}";
		await WithUnitOfWorkAsync(() => Copies.InsertAsync(new(Guid.NewGuid(), foreign.Catalog.BookId, 2, accession,
			context.Catalog.SectionId, context.Catalog.ShelfId, GetRequiredService<ICurrentTenant>().Id), true));
		error = await Record.ExceptionAsync(() => Lookups.GetCopyAsync(accession));
		(error is BusinessException or EntityNotFoundException).ShouldBeTrue();
	}

	[Theory]
	[InlineData(false, ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData(false, ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	[InlineData(true, ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData(true, ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	public async Task Canonical_Lookups_Should_Require_View_And_Their_Exact_Action(bool returning, string denied)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await SeedAsync();
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(denied).Returns(false);
		if (returning) await Should.ThrowAsync<AbpAuthorizationException>(() => Lookups.GetReturnAsync("unknown"));
		else await Should.ThrowAsync<AbpAuthorizationException>(() => Lookups.GetCopyAsync("unknown"));
	}

	private ILibraryCirculationLookupAppService Lookups => GetRequiredService<ILibraryCirculationLookupAppService>();
	private ILibraryLoanAppService Loans => GetRequiredService<ILibraryLoanAppService>();
	private IRepository<BookCopy, Guid> Copies => GetRequiredService<IRepository<BookCopy, Guid>>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync() => WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));

	private Task<LoanContext> SeedLoanAsync(LibraryCopyLifecycleTestContext context, bool historic = false, string? corruption = null) => WithUnitOfWorkAsync(async () =>
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var year = historic ? context.Catalog.OriginYearId : context.Catalog.YearId;
		var boardId = context.BoardId; var mediumId = context.MediumId; var stageId = context.StageId; var standardId = context.BorrowerClassId;
		if (historic)
		{
			var board = new EducationBoard(Guid.NewGuid(), tenant, EducationBoardType.CBSE, year);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), tenant, "English", board.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), tenant, "Previous stage", null, medium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var standard = new Standard(Guid.NewGuid(), tenant, "4", 9, stage.Id, null);
			await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			boardId = board.Id; mediumId = medium.Id; stageId = stage.Id; standardId = standard.Id;
		}
		string Snapshot(int version, Guid personId, int role) => JsonSerializer.Serialize(new
		{
			Version = version, Id = personId, Role = role, MemberName = "Borrower at checkout", MemberIdentifier = "SAVED-CARD", ClassDivision = "4-A",
			AcademicYearId = year, AcademicContext = new { AcademicBoardId = boardId, AcademicMediumId = mediumId,
				AcademicMediumStageId = stageId, StandardId = standardId, DivisionId = (Guid?)null }
		});
		var snapshot = Snapshot(1, context.StudentId, 0);
		var copy = await Copies.GetAsync(context.Catalog.CopyId);
		var policy = JsonSerializer.Serialize(new LibraryBorrowingPolicySnapshotDto { IsLegacyManualDate = true, Role = 0,
			Terms = new() { MaxBooksAtTime = 4, ReservationLimit = 1 } });
		var date = new DateOnly(historic ? 2025 : 2026, 11, 17);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, copy.Id, copy.AccessionNumber, context.StudentId, 0, date, date.AddDays(7),
			new(year, boardId, mediumId, stageId, standardId, null, policy, snapshot, "{}", GetRequiredService<ICurrentUser>().Id!.Value,
				new DateTimeOffset(date.Year, 11, 17, 9, 0, 0, TimeSpan.Zero)));
		var repository = GetRequiredService<IRepository<BookIssue, Guid>>();
		await repository.InsertAsync(loan, true);
		if (corruption != null)
		{
			// Simulate persisted legacy corruption after validating new-loan evidence through the domain factory.
			var corrupted = corruption == "Malformed" ? "{invalid" : Snapshot(corruption == "UnknownVersion" ? 99 : 1,
				corruption == "WrongPerson" ? Guid.NewGuid() : context.StudentId, corruption == "WrongRole" ? 1 : 0);
			typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerSnapshotJson))!.SetValue(loan, corrupted);
			await repository.UpdateAsync(loan, true);
		}
		copy.Status = BookCopyStatus.Issued;
		await Copies.UpdateAsync(copy, true);
		return new LoanContext(loan.Id, copy.AccessionNumber, year, boardId, mediumId, stageId, standardId);
	});

	private Task RestrictAsync(LibraryCopyLifecycleTestContext context, LoanContext loan, bool historic) => WithUnitOfWorkAsync(async () =>
	{
		var user = GetRequiredService<ICurrentUser>().Id!.Value;
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.UserId == user, true);
		await scopes.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: user,
			academicYearId: historic ? loan.YearId : context.Catalog.YearId,
			educationBoardId: historic ? loan.BoardId : context.BoardId, educationMediumId: historic ? loan.MediumId : context.MediumId,
			educationMediumStageId: historic ? loan.StageId : context.StageId, standardId: historic ? loan.StandardId : context.BorrowerClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(user);
	});
	private sealed record LoanContext(Guid LoanId, string Accession, Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid StandardId);
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryCanonicalLookupTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View).Returns(false);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}
