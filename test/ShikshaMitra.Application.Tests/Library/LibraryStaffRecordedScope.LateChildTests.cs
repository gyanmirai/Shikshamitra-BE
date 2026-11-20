using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task A_New_Empty_Assignment_Capture_Should_Save_An_Exact_Zero_Count_And_Remain_Unclassified()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
			.DeleteAsync(row => row.EmployeeId == fixture.EmployeeId, true));
		var group = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
			Guid.NewGuid(), fixture.EmployeeId, fixture.Context.Catalog.YearId, LibraryBorrowerScopeProvenance.CapturedAtOperation,
			LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value)));

		group.ExpectedAssignmentCount.ShouldBe(0);
		group.IsComplete.ShouldBeFalse();
		group.IncompleteReason.ShouldNotBeNullOrWhiteSpace();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().CountAsync(row => row.CaptureId == group.Id)).ShouldBe(0));
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(async () => await (await GetRequiredService<LibraryRecordedStaffScopeQuery>()
			.GetReadableCapturesAsync(scope)).AnyAsync(row => row.Id == group.Id))).ShouldBeFalse();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	public async Task A_New_Capture_Should_Reject_An_Added_Child_Set_That_Differs_From_Its_Sealed_Count(int count)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
		{
			var assignments = await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().GetListAsync(row => row.EmployeeId == fixture.EmployeeId);
			var group = new LibraryBorrowerScopeCapture(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id!.Value,
				LibraryBorrowerScopeSourceKind.Loan, Guid.NewGuid(), fixture.EmployeeId, fixture.Context.Catalog.YearId, 1,
				LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value),
				new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero), assignments.Select(row => new LibraryBorrowerAssignmentEvidence(
					row.Id, row.EducationMediumStageId, fixture.Context.MediumId, fixture.Context.BoardId, fixture.Context.Catalog.YearId)).ToArray());
			typeof(LibraryBorrowerScopeCapture).GetProperty(nameof(LibraryBorrowerScopeCapture.ExpectedAssignmentCount))!.SetValue(group, count);
			await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().InsertAsync(group, true);
		}));
	}

	[Fact]
	public async Task Persisted_Staff_Capture_Should_Reject_Adding_A_New_Assignment_Child()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await MoveStaffAsync(fixture);
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
		{
			var assignment = (await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
				.GetListAsync(row => row.EmployeeId == fixture.EmployeeId)).Single();
			var child = new LibraryBorrowerAssignmentSnapshot(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id!.Value,
				loan.BorrowerScopeCaptureId!.Value, new(assignment.Id, fixture.LaterStageId, fixture.Context.MediumId,
					fixture.Context.BoardId, fixture.Context.Catalog.YearId));
			await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().InsertAsync(child, true);
		}));
		await SetReaderStageAsync(fixture, fixture.LaterStageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeFalse();
	}

	[Fact]
	public async Task Unexpected_Staff_Capture_Child_Count_Should_Not_Grant_A_New_Historical_Stage()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await MoveStaffAsync(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var assignment = (await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
				.GetListAsync(row => row.EmployeeId == fixture.EmployeeId)).Single();
			await db.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppLibraryBorrowerAssignmentSnapshots"
				("Id", "TenantId", "CaptureId", "OrganizationAssignmentId", "StageId", "MediumId", "BoardId", "AcademicYearId")
				VALUES ({Guid.NewGuid()}, {GetRequiredService<ICurrentTenant>().Id}, {loan.BorrowerScopeCaptureId},
				{assignment.Id}, {fixture.LaterStageId}, {fixture.Context.MediumId}, {fixture.Context.BoardId}, {fixture.Context.Catalog.YearId})
				""");
		});
		await SetReaderStageAsync(fixture, fixture.LaterStageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();

		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeFalse();
	}
}
