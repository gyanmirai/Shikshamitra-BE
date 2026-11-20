using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationSchoolBoundary_Tests : ShikshaMitraApplicationTestBase<LibraryReservationSchoolBoundaryTestModule>
{
	[Theory]
	[InlineData("Preview")]
	[InlineData("Create")]
	[InlineData("Legacy")]
	public async Task Reservation_Entry_Points_Should_Deny_An_Actual_Host_Principal_Before_Any_Operational_Write(string entry)
	{
		using var host = GetRequiredService<ICurrentTenant>().Change(null);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString())
		], "LibraryReservationHostBoundary")));
		var fixture = await SeedAsync();
		GetRequiredService<ICurrentTenant>().Id.ShouldBeNull();
		GetRequiredService<ICurrentUser>().TenantId.ShouldBeNull();
		(await GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)).ShouldBeTrue();
		(await GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)).ShouldBeTrue();
		var before = await StateAsync(fixture);

		var error = await Record.ExceptionAsync(async () =>
		{
			if (entry == "Preview") await App.PreviewAsync(Input(fixture));
			else if (entry == "Create") await App.CreateAsync(Input(fixture));
			else await GetRequiredService<IBookAppService>().ReserveAsync(fixture.Catalog.BookId, new()
			{
				MemberId = fixture.StudentId, AcademicYearId = fixture.Catalog.YearId,
				RequestedDate = new DateTime(2026, 11, 17, 18, 45, 0, DateTimeKind.Utc),
				ReserveDueDate = new DateTime(2026, 11, 20, 4, 15, 0, DateTimeKind.Utc)
			});
		});

		(await StateAsync(fixture)).ShouldBe(before);
		error.ShouldBeOfType<BusinessException>().Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task An_Actual_School_Should_Create_A_Manual_Canonical_Request_Without_A_Configured_Library_Timezone()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedAsync();
		(await GetRequiredService<LibraryCalendarService>().GetSettingsAsync()).NeedsConfiguration.ShouldBeTrue();

		var request = await App.CreateAsync(Input(fixture));

		request.RequestedDate.ShouldBe(new DateOnly(2026, 11, 17));
		request.RequestExpiryDate.ShouldBe(new DateOnly(2026, 11, 20));
		request.RequestedDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.ExplicitSchoolDate);
		request.LegacyRequestedDate.ShouldBeNull();
		request.LegacyReserveDueDate.ShouldBeNull();
		request.AcademicYearId.ShouldBe(fixture.Catalog.YearId);
	}

	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	public async Task Accepted_Legacy_Manual_Requests_Should_Keep_Their_Submitted_Date_Components_And_Raw_Values(DateTimeKind kind)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedAsync();
		var requested = new DateTime(2026, 11, 17, 18, 45, 0, kind);
		var expiry = new DateTime(2026, 11, 20, 4, 15, 0, kind);

		var created = await GetRequiredService<IBookAppService>().ReserveAsync(fixture.Catalog.BookId, new()
		{
			MemberId = fixture.StudentId, AcademicYearId = fixture.Catalog.YearId, RequestedDate = requested, ReserveDueDate = expiry
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(Guid.Parse(created.Id));
			stored.RequestedDate.ShouldBe(requested);
			stored.ReserveDueDate.ShouldBe(expiry);
			stored.SchoolRequestedDate.ShouldBe(new DateOnly(2026, 11, 17));
			stored.SchoolRequestExpiryDate.ShouldBe(new DateOnly(2026, 11, 20));
			stored.RequestedDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
			stored.RequestExpiryDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
			stored.AcademicYearId.ShouldBe(fixture.Catalog.YearId);
		});
	}

	[Fact]
	public async Task Confirming_A_Library_Timezone_Should_Not_Confirm_Or_Expire_An_Old_Stored_Raw_Request()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedAsync();
		var requested = new DateTime(2001, 1, 1, 18, 45, 0);
		var expiry = new DateTime(2001, 1, 8, 4, 15, 0);
		var id = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(new(id, fixture.Catalog.BookId,
				fixture.StudentId, requested, expiry, GetRequiredService<ICurrentTenant>().Id), true);
			await GetRequiredService<ISettingManager>().SetForTenantAsync(GetRequiredService<ICurrentTenant>().Id!.Value,
				LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata", forceToSet: true);
		});

		await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>().RebalanceAsync(fixture.Catalog.BookId));
		var request = await App.GetAsync(id, fixture.Catalog.YearId);

		request.LegacyRequestedDate.ShouldBe(requested);
		request.LegacyReserveDueDate.ShouldBe(expiry);
		request.RequestedDate.ShouldBeNull();
		request.RequestExpiryDate.ShouldBeNull();
		request.RequestedDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.UnconfirmedStoredComponent);
		request.RequestExpiryDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.UnconfirmedStoredComponent);
		request.State.ShouldBe((int)LibraryReservationState.NeedsReview);
		request.Hold.ShouldBeNull();
	}

	private ILibraryReservationAppService App => GetRequiredService<ILibraryReservationAppService>();
	private Task<SchoolFixture> SeedAsync() => WithUnitOfWorkAsync(async () =>
	{
		if (GetRequiredService<ICurrentTenant>().Id.HasValue)
		{
			var school = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
			return new SchoolFixture(school.Catalog, school.StudentId);
		}
		var catalog = await LibraryPolicyTestFixture.SeedAsync(ServiceProvider);
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var board = new EducationBoard(Guid.NewGuid(), tenant, EducationBoardType.CBSE, catalog.YearId);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), tenant, "Host metadata medium", board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenant, "Manual", null, "Borrower", catalog.YearId,
			board.Id, medium.Id, Guid.NewGuid(), null, null, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999999");
		// Represent an existing host registration with an unknown optional stage, without another school's hierarchy.
		typeof(Student).GetProperty(nameof(Student.AcademicMediumStageId))!.SetValue(student, null);
		student.AdditionalDetail!.IsLibraryMember = true;
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(new(Guid.NewGuid(), tenant, student.Id, "MANUAL-CARD"), true);
		(await GetRequiredService<LibraryReservationEligibilityReader>().RequireAsync(student.Id, catalog.YearId)).Eligibility.ShouldBe(LibraryReservationEligibility.Eligible);
		return new SchoolFixture(catalog, student.Id);
	});
	private static LibraryReservationCreateRequest Input(SchoolFixture fixture) => new()
	{
		BookId = fixture.Catalog.BookId, AcademicYearId = fixture.Catalog.YearId, MemberId = fixture.StudentId,
		RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 11, 20)
	};
	private Task<StoredState> StateAsync(SchoolFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		return new StoredState(
			await GetRequiredService<IRepository<BookReservation, Guid>>().CountAsync(row => row.TenantId == tenant),
			await GetRequiredService<IRepository<LibraryHold, Guid>>().CountAsync(row => row.TenantId == tenant),
			await GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().CountAsync(row => row.TenantId == tenant),
			await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().CountAsync(row => row.TenantId == tenant),
			await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().CountAsync(row => row.TenantId == tenant),
			await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().CountAsync(row => row.TenantId == tenant),
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).Status);
	});
	private sealed record SchoolFixture(LibraryPolicyTestContext Catalog, Guid StudentId);
	private sealed record StoredState(int Requests, int Holds, int ReservationEvents, int CopyEvents, int Captures, int Assignments, BookCopyStatus CopyStatus);
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryReservationSchoolBoundaryTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve).Returns(true);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}
