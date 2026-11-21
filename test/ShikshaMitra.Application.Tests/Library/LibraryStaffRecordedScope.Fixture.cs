using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	private async Task<StaffScopeFixture> SeedStaffAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var context = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
			var tenantId = GetRequiredService<ICurrentTenant>().Id;
			var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
			var role = new IdentityRole(Guid.NewGuid(), $"library-staff-{Guid.NewGuid():N}", tenantId);
			await roles.InsertAsync(role, true);
			var suffix = Guid.NewGuid().ToString("N");
			var employee = new Employee(Guid.NewGuid(), tenantId, null, "STAFF-" + suffix,
				new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Borrower", FirstName: "Staff", MiddleName: null,
					ShortName: null, EmployeeStatusType: EmployeeStatusType.Active, DateOfBirth: null, FamilyCast: null,
					CastType: null, GenderType: null, Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: null,
					SecondaryMobileNo: null, Email: $"{suffix}@example.test", PanNo: suffix[..10].ToUpperInvariant(),
					AdharNo: suffix[10..22], Address1: null, Address2: null, Address3: null, Department: null,
					Designation: "Teacher", DateOfJoining: null, EmployeeGroup: EmployeeGroupType.Teaching, Qualification: null,
					RoleId: role.Id, IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null), null, null);
			await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, true);
			await GetRequiredService<IRepository<EmployeeAdditionalDetails, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId,
				employee.Id, null, false, null, true, false), true);
			await GetRequiredService<IRepository<BorrowingRule, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId,
				context.Catalog.YearId, 1, 4, 2), true);
			var stages = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
			var second = new EducationMediumStage(Guid.NewGuid(), tenantId, "Second recorded stage", null, context.MediumId);
			var later = new EducationMediumStage(Guid.NewGuid(), tenantId, "Later staff stage", null, context.MediumId);
			await stages.InsertManyAsync([second, later], true);
			var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await assignments.InsertManyAsync([
				new(Guid.NewGuid(), tenantId, employee.Id, context.StageId), new(Guid.NewGuid(), tenantId, employee.Id, second.Id)
			], true);
			return new StaffScopeFixture(context, employee.Id, second.Id, later.Id);
		});
	}

	private async Task<LibraryLoanDto> CheckoutStaffAsync(StaffScopeFixture fixture)
	{
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = fixture.Context.Catalog.CopyId, MemberId = fixture.EmployeeId, AcademicYearId = fixture.Context.Catalog.YearId,
			IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 24)
		});
		var result = await GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(new()
		{
			CopyId = plan.CopyId, MemberId = fixture.EmployeeId, AcademicYearId = plan.AcademicYearId, IssueDate = plan.IssueDate,
			RequestedDueDate = plan.DueDate, Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		return result.Loan;
	}

	private Task MoveStaffAsync(StaffScopeFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.EmployeeId == fixture.EmployeeId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
			fixture.EmployeeId, fixture.LaterStageId), true);
	});

	private Task SetReaderStageAsync(StaffScopeFixture fixture, Guid stageId) => WithUnitOfWorkAsync(async () =>
	{
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == actor, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
			academicYearId: fixture.Context.Catalog.YearId, educationBoardId: fixture.Context.BoardId, educationMediumId: fixture.Context.MediumId,
			educationMediumStageId: stageId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	});

	private sealed record StaffScopeFixture(LibraryCopyLifecycleTestContext Context, Guid EmployeeId, Guid SecondStageId, Guid LaterStageId);
}
