using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private Task<(Guid, Guid?)> SeedNotificationCandidateStaffAsync(Context context, NotificationClassContext placement) =>
		WithUnitOfWorkAsync<(Guid, Guid?)>(async () =>
		{
			var role = new IdentityRole(Guid.NewGuid(), $"candidate-staff-{Guid.NewGuid():N}", context.TenantId);
			await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(role, true);
			var suffix = Guid.NewGuid().ToString("N");
			var employee = new Employee(Guid.NewGuid(), context.TenantId, null, "STAFF-" + suffix,
				new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Borrower", FirstName: "Recorded", MiddleName: null,
					ShortName: null, EmployeeStatusType: EmployeeStatusType.Active, DateOfBirth: null, FamilyCast: null,
					CastType: null, GenderType: null, Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: null,
					SecondaryMobileNo: null, Email: $"{suffix}@example.test", PanNo: suffix[..10].ToUpperInvariant(),
					AdharNo: suffix[10..22], Address1: null, Address2: null, Address3: null, Department: null,
					Designation: "Teacher", DateOfJoining: null, EmployeeGroup: EmployeeGroupType.Teaching, Qualification: null,
					RoleId: role.Id, IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null), null, null);
			await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, true);
			await GetRequiredService<IRepository<EmployeeAdditionalDetails, Guid>>().InsertAsync(new(Guid.NewGuid(), context.TenantId,
				employee.Id, null, false, null, true, false), true);
			await GetRequiredService<IRepository<BorrowingRule, Guid>>().InsertAsync(new(Guid.NewGuid(), context.TenantId,
				placement.YearId, 1, 4, 2), true);
			var second = new EducationMediumStage(Guid.NewGuid(), context.TenantId, "Second original staff stage", null, placement.MediumId);
			var laterBoard = new EducationBoard(Guid.NewGuid(), context.TenantId, EducationBoardType.GSEB, placement.YearId);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(laterBoard, true);
			var laterMedium = new EducationMedium(Guid.NewGuid(), context.TenantId, "Replacement staff medium", laterBoard.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(laterMedium, true);
			var later = new EducationMediumStage(Guid.NewGuid(), context.TenantId, "Replacement staff stage", null, laterMedium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertManyAsync([second, later], true);
			await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().InsertManyAsync([
				new(Guid.NewGuid(), context.TenantId, employee.Id, placement.StageId), new(Guid.NewGuid(), context.TenantId, employee.Id, second.Id)
			], true);
			return (employee.Id, later.Id);
		});

	private Task MoveNotificationCandidateStaffAsync(RecordedCandidate fixture) => WithUnitOfWorkAsync(async () =>
	{
		var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.EmployeeId == fixture.MemberId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), fixture.Context.TenantId, fixture.MemberId, fixture.LaterStaffStageId!.Value), true);
	});

	private Task SetNotificationCandidateStaffScopeAsync(RecordedCandidate fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var stageId = currentPlacement ? fixture.LaterStaffStageId!.Value : fixture.Placement.StageId;
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(stage.EducationMediumId);
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == fixture.Context.ActorId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), fixture.Context.TenantId, userId: fixture.Context.ActorId,
			academicYearId: fixture.Placement.YearId, educationBoardId: medium.EducationBoardId, educationMediumId: medium.Id,
			educationMediumStageId: stage.Id), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(fixture.Context.ActorId);
	});
}
