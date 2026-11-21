using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCirculationCanonicalRead_Tests : ShikshaMitraApplicationTestBase<LibraryCanonicalLookupCalendarTestModule>
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task List_And_Copy_History_Should_Read_Canonical_Dates_And_Confirmed_School_Day_Without_Fabricating_Raw_Timestamps(bool history)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await AddCivilAsync(context, Current(context));
		await SetZoneAsync();

		var result = history ? await History.GetAsync(context.Catalog.BookId, loan.CopyId) : await Reads.GetListAsync();

		var row = result.Items.Single();
		row.TransactionId.ShouldBe(loan.LoanId);
		row.AcademicYearId.ShouldBe(context.Catalog.YearId);
		row.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 1));
		row.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 17));
		row.IssueDateProvenance.ShouldBe(1);
		row.DueDateProvenance.ShouldBe(1);
		row.RawIssueDate.ShouldBeNull();
		row.RawDueDate.ShouldBeNull();
		row.IssueDate.ShouldBeNull();
		row.DueDate.ShouldBeNull();
		row.IsOverdue.ShouldBe(true);
		row.OverdueAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
		row.OverdueDateSource.ShouldBe("ConfirmedLibraryClock");
		row.StoredStatus.ShouldBe((int)BookIssueStatus.Issued);
		row.Status.ShouldBe((int)BookIssueStatus.Overdue);
	}

	[Fact]
	public async Task Unconfigured_School_Clock_Should_Leave_Overdue_Unclassified_Instead_Of_Using_Utc()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await AddCivilAsync(context, Current(context));

		var row = (await Reads.GetListAsync()).Items.Single();

		row.IsOverdue.ShouldBeNull();
		row.OverdueAsOfDate.ShouldBeNull();
		row.OverdueDateSource.ShouldBe("Unavailable");
		row.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		row.Status.ShouldBe((int)BookIssueStatus.Issued);
	}

	[Fact]
	public async Task Manual_School_Should_Classify_Confirmed_Due_Date_Against_Explicit_Read_AsOf_Date()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await AddCivilAsync(context, Current(context));

		var row = (await Reads.GetListAsync(asOfDate: new DateOnly(2026, 11, 18))).Items.Single();

		row.IsOverdue.ShouldBe(true);
		row.OverdueAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
		row.OverdueDateSource.ShouldBe("ExplicitSchoolDate");
		row.OverdueUnavailableReason.ShouldBeNull();
	}

	[Fact]
	public async Task Raw_Legacy_Past_Due_Date_Should_Remain_Exact_And_Unclassified_Even_With_Confirmed_Clock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var rawIssue = new DateTime(2001, 1, 1, 21, 45, 0);
		var rawDue = new DateTime(2001, 1, 8, 3, 15, 0);
		await AddLegacyAsync(context, context.StudentId, rawIssue, rawDue);
		await SetZoneAsync();

		var row = (await Reads.GetListAsync()).Items.Single();

		row.RawIssueDate.ShouldBe(rawIssue);
		row.RawDueDate.ShouldBe(rawDue);
		row.IssueDate.ShouldBe(rawIssue);
		row.DueDate.ShouldBe(rawDue);
		row.SchoolIssueDate.ShouldBeNull();
		row.SchoolDueDate.ShouldBeNull();
		row.DueDateProvenance.ShouldBe(0);
		row.IsOverdue.ShouldBeNull();
		row.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		row.Status.ShouldBe((int)BookIssueStatus.Issued);
	}

	[Fact]
	public async Task A_Civil_Field_With_Unconfirmed_Provenance_Should_Not_Be_Treated_As_A_Confirmed_Deadline()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await AddCivilAsync(context, Current(context));
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await Loans.GetAsync(loan.LoanId);
			typeof(BookIssue).GetProperty(nameof(BookIssue.DueDateProvenance))!.SetValue(row, ShikshaMitra.Library.Shared.LibraryCivilDateProvenance.UnconfirmedStoredComponent);
			await Loans.UpdateAsync(row, true);
		});

		var result = (await Reads.GetListAsync(asOfDate: new DateOnly(2026, 11, 18))).Items.Single();

		result.IsOverdue.ShouldBeNull();
		result.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Stored_Hierarchy_And_Year_Should_Filter_Counts_And_Pages_After_The_Borrower_Has_Moved(bool originalScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var original = await OriginalAsync(context);
		var old1 = await AddCivilAsync(context, original);
		var old2 = await AddCivilAsync(context, original);
		var current = await AddCivilAsync(context, Current(context));
		await RestrictAsync(originalScope ? original : Current(context));

		var first = await Reads.GetListAsync(pageSize: 1);
		var second = await Reads.GetListAsync(page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(originalScope ? 2 : 1);
		first.TotalPages.ShouldBe(originalScope ? 2 : 1);
		if (originalScope)
		{
			new[] { first.Items.Single().TransactionId, second.Items.Single().TransactionId }.Order().ShouldBe(new[] { old1.LoanId, old2.LoanId }.Order());
			first.Items.Single().MemberName.ShouldBe("Name recorded at checkout");
			first.Items.Single().MemberDisplaySource.ShouldBe("LoanSnapshot");
			(await History.GetAsync(context.Catalog.BookId, old1.CopyId)).Items.Single().TransactionId.ShouldBe(old1.LoanId);
		}
		else
		{
			first.Items.Single().TransactionId.ShouldBe(current.LoanId);
			second.Items.ShouldBeEmpty();
			(await History.GetAsync(context.Catalog.BookId, old1.CopyId)).TotalCount.ShouldBe(0);
		}
	}

	[Fact]
	public async Task Optional_Year_Should_Filter_The_Recorded_Transaction_Year_And_Reject_Unknown_Or_Unauthorized_Years()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var original = await OriginalAsync(context);
		await AddCivilAsync(context, original);
		var current = await AddCivilAsync(context, Current(context));

		(await Reads.GetListAsync()).TotalCount.ShouldBe(2);
		var selected = await Reads.GetListAsync(academicYearId: context.Catalog.YearId);
		selected.TotalCount.ShouldBe(1);
		selected.Items.Single().TransactionId.ShouldBe(current.LoanId);
		await Should.ThrowAsync<BusinessException>(() => Reads.GetListAsync(academicYearId: Guid.NewGuid()));
		await RestrictAsync(Current(context));
		await Should.ThrowAsync<BusinessException>(() => Reads.GetListAsync(academicYearId: original.YearId));
	}

	[Fact]
	public async Task Alias_Loan_Should_Use_Validated_Saved_Person_Labels_Instead_Of_Current_Profile_Labels()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await AddCivilAsync(context, Current(context), alias: true);

		var row = (await Reads.GetListAsync()).Items.Single();

		row.MemberId.ShouldBe(context.StudentId);
		row.MemberName.ShouldBe("Name recorded at checkout");
		row.MemberIdentifier.ShouldBe("SAVED-CARD");
		row.ClassDivision.ShouldBe("Recorded class");
		row.MemberDisplaySource.ShouldBe("LoanSnapshot");
		row.ContactNo.ShouldBeNull();
	}

	[Theory]
	[InlineData("Malformed")]
	[InlineData("WrongRole")]
	public async Task Invalid_Snapshot_Should_Withhold_Current_Out_Of_Scope_Labels_While_Keeping_The_Readable_Loan(string corruption)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var original = await OriginalAsync(context);
		var loan = await AddCivilAsync(context, original, corruption: corruption);
		await RestrictAsync(original);

		var row = (await Reads.GetListAsync()).Items.Single();

		row.TransactionId.ShouldBe(loan.LoanId);
		row.MemberName.ShouldBeNull();
		row.MemberIdentifier.ShouldBeNull();
		row.ClassDivision.ShouldBeNull();
		row.MemberDisplaySource.ShouldBe("Unavailable");
		row.ContactNo.ShouldBeNull();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Unknown_Legacy_Requester_Should_Be_Visible_For_Tenantwide_Review_Only_And_Never_Cross_Tenants(bool tenantwide)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var legacy = await AddLegacyAsync(context, Guid.NewGuid(), new DateTime(2020, 1, 1), new DateTime(2020, 1, 8));
		if (!tenantwide) await RestrictAsync(Current(context));
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			var foreign = await SeedAsync();
			await AddLegacyAsync(foreign, context.StudentId, new DateTime(2020, 1, 1), new DateTime(2020, 1, 8));
		}

		var result = await Reads.GetListAsync();

		result.TotalCount.ShouldBe(tenantwide ? 1 : 0);
		if (tenantwide)
		{
			result.Items.Single().TransactionId.ShouldBe(legacy.LoanId);
			result.Items.Single().MemberName.ShouldBeNull();
			result.Items.Single().MemberDisplaySource.ShouldBe("Unavailable");
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Both_Read_Paths_Should_Require_The_Actual_View_Grant(bool history)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var loan = await AddCivilAsync(context, Current(context));
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View).Returns(false);

		if (history) await Should.ThrowAsync<AbpAuthorizationException>(() => History.GetAsync(context.Catalog.BookId, loan.CopyId));
		else await Should.ThrowAsync<AbpAuthorizationException>(() => Reads.GetListAsync());
	}

	[Fact]
	public async Task Canonical_Operational_Read_Should_Require_An_Actual_School_Tenant_Even_With_Host_Scope_And_Grants()
	{
		using var host = GetRequiredService<ICurrentTenant>().Change(null);
		await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));

		await Should.ThrowAsync<BusinessException>(() => Reads.GetListAsync());
	}

	[Fact]
	public async Task Current_Label_Fallback_Should_Read_Only_Actual_InScope_Scalar_Facts_Without_Contacts_Or_Student_Graph()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await AddCivilAsync(context, Current(context), corruption: "Malformed");
		using var activity = new Activity("Canonical circulation list labels").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);

		var row = (await Reads.GetListAsync()).Items.Single();

		string.Join(" ", (row.MemberName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ShouldBe("Library Borrower");
		row.MemberDisplaySource.ShouldBe("CurrentAuthorizedRecord");
		row.ContactNo.ShouldBeNull();
		var queries = commands.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
		queries.ShouldNotBeEmpty();
		foreach (var sql in queries)
		{
			sql.ShouldNotContain("StudentFamilyDetails");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppStudentDocuments");
		}
	}

	private ILibraryCirculationReadAppService Reads => GetRequiredService<ILibraryCirculationReadAppService>();
	private ILibraryCopyHistoryAppService History => GetRequiredService<ILibraryCopyHistoryAppService>();
	private IRepository<BookIssue, Guid> Loans => GetRequiredService<IRepository<BookIssue, Guid>>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync() => WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
	private Task SetZoneAsync() => WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(
		GetRequiredService<ICurrentTenant>().Id!.Value, LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata", forceToSet: true));
	private static Placement Current(LibraryCopyLifecycleTestContext context) => new(context.Catalog.YearId, context.BoardId, context.MediumId, context.StageId, context.BorrowerClassId);
	private Task<Placement> OriginalAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var board = new EducationBoard(Guid.NewGuid(), tenant, EducationBoardType.CBSE, context.Catalog.OriginYearId);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), tenant, "Original medium", board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), tenant, "Original stage", null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = new Standard(Guid.NewGuid(), tenant, "4", 9, stage.Id, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		return new Placement(context.Catalog.OriginYearId, board.Id, medium.Id, stage.Id, standard.Id);
	});
	private Task<LoanIdentity> AddCivilAsync(LibraryCopyLifecycleTestContext context, Placement placement, bool alias = false, string? corruption = null) => WithUnitOfWorkAsync(async () =>
	{
		var copy = await AddCopyAsync(context);
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var member = context.StudentId;
		if (alias)
		{
			var membership = new LibraryMember(Guid.NewGuid(), tenant, member, "CURRENT-CARD");
			await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(membership, true);
			member = membership.Id;
		}
		string Snapshot(int role) => JsonSerializer.Serialize(new { Version = 1, Id = context.StudentId, Role = role, AcademicYearId = placement.YearId,
			MemberName = "Name recorded at checkout", MemberIdentifier = "SAVED-CARD", ClassDivision = "Recorded class",
			AcademicContext = new { AcademicBoardId = placement.BoardId, AcademicMediumId = placement.MediumId,
				AcademicMediumStageId = placement.StageId, StandardId = placement.ClassId, DivisionId = (Guid?)null } });
		var year = placement.YearId == context.Catalog.YearId ? 2026 : 2025;
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, copy.Id, copy.AccessionNumber, member, 0,
			new DateOnly(year, 11, 1), new DateOnly(year, 11, 17), new(placement.YearId, placement.BoardId, placement.MediumId,
				placement.StageId, placement.ClassId, null, "{}", Snapshot(0), "{}", GetRequiredService<ICurrentUser>().Id!.Value,
				new DateTimeOffset(year, 11, 1, 9, 0, 0, TimeSpan.Zero)));
		await Loans.InsertAsync(loan, true);
		if (corruption != null)
		{
			typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerSnapshotJson))!.SetValue(loan, corruption == "Malformed" ? "{invalid" : Snapshot(1));
			await Loans.UpdateAsync(loan, true);
		}
		return new LoanIdentity(loan.Id, copy.Id);
	});
	private Task<LoanIdentity> AddLegacyAsync(LibraryCopyLifecycleTestContext context, Guid memberId, DateTime issueDate, DateTime dueDate) => WithUnitOfWorkAsync(async () =>
	{
		var copy = await AddCopyAsync(context);
		var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, memberId, 0, issueDate, dueDate, GetRequiredService<ICurrentTenant>().Id);
		await Loans.InsertAsync(loan, true);
		return new LoanIdentity(loan.Id, copy.Id);
	});
	private async Task<BookCopy> AddCopyAsync(LibraryCopyLifecycleTestContext context)
	{
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var number = await copies.CountAsync(row => row.BookId == context.Catalog.BookId) + 1;
		var copy = new BookCopy(Guid.NewGuid(), context.Catalog.BookId, number, $"READ-{Guid.NewGuid():N}",
			context.Catalog.SectionId, context.Catalog.ShelfId, GetRequiredService<ICurrentTenant>().Id);
		return await copies.InsertAsync(copy, true);
	}
	private Task RestrictAsync(Placement placement) => WithUnitOfWorkAsync(async () =>
	{
		var user = GetRequiredService<ICurrentUser>().Id!.Value;
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.UserId == user, true);
		await scopes.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: user, academicYearId: placement.YearId,
			educationBoardId: placement.BoardId, educationMediumId: placement.MediumId, educationMediumStageId: placement.StageId, standardId: placement.ClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(user);
	});
	private sealed record Placement(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid ClassId);
	private sealed record LoanIdentity(Guid LoanId, Guid CopyId);
}
