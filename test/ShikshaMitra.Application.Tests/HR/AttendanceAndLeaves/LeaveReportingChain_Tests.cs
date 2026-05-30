using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.ReportingManager;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveReportingChain_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly LeaveValidationService _leaveValidation;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfo;
	private readonly IReportingManagerAppService _reportingManager;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public LeaveReportingChain_Tests()
	{
		_leaveValidation = GetRequiredService<LeaveValidationService>();
		_employeePersonalInfo = GetRequiredService<IEmployeePersonalInfoAppService>();
		_reportingManager = GetRequiredService<IReportingManagerAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task EnsureActorMayChangeLeaveStatus_Should_Allow_Direct_Manager()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (managerId, reporteeId) = await CreateManagerAndReporteeAsync();
			await _leaveValidation.EnsureActorMayChangeLeaveStatusAsync(reporteeId, managerId);
		});
	}

	[Fact]
	public async Task EnsureActorMayChangeLeaveStatus_Should_Allow_Skip_Level_Manager()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (topId, midId, leafId) = await CreateThreeLevelChainAsync();
			await _leaveValidation.EnsureActorMayChangeLeaveStatusAsync(leafId, topId);
			await _leaveValidation.EnsureActorMayChangeLeaveStatusAsync(leafId, midId);
		});
	}

	[Fact]
	public async Task EnsureActorMayChangeLeaveStatus_Should_Reject_Self()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (_, reporteeId) = await CreateManagerAndReporteeAsync();
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveValidation.EnsureActorMayChangeLeaveStatusAsync(reporteeId, reporteeId));
			ex.Code.ShouldBe(HRErrorCodes.LeaveApproverNotInReportingChain);
		});
	}

	[Fact]
	public async Task EnsureActorMayChangeLeaveStatus_Should_Reject_Unrelated_Employee()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (_, reporteeId) = await CreateManagerAndReporteeAsync();
			var stranger = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"stranger.{Guid.NewGuid():N}@t.com"));
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveValidation.EnsureActorMayChangeLeaveStatusAsync(reporteeId, stranger.Id));
			ex.Code.ShouldBe(HRErrorCodes.LeaveApproverNotInReportingChain);
		});
	}

	[Fact]
	public async Task AssignReportingManager_Should_Reject_Inactive_Manager()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = await _employeePersonalInfo.CreateAsync(
				await BuildCreateDtoAsync($"inactive.mgr.{Guid.NewGuid():N}@t.com", EmployeeStatusType.Inactive));
			var reportee = await _employeePersonalInfo.CreateAsync(
				await BuildCreateDtoAsync($"inactive.rep.{Guid.NewGuid():N}@t.com"));

			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _reportingManager.AssignReportingManagerAsync(reportee.Id, new AssignReportingManagerDto
				{
					ReportingManagerId = manager.Id
				}));

			ex.Code.ShouldBe("ShikshaMitra:ReportingManagerMustBeActive");
		});
	}

	private async Task<(Guid ManagerId, Guid ReporteeId)> CreateManagerAndReporteeAsync()
	{
		var manager = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"mgr.{Guid.NewGuid():N}@t.com"));
		var reportee = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"rep.{Guid.NewGuid():N}@t.com"));
		await _reportingManager.AssignReportingManagerAsync(reportee.Id, new AssignReportingManagerDto
		{
			ReportingManagerId = manager.Id
		});
		return (manager.Id, reportee.Id);
	}

	private async Task<(Guid TopId, Guid MidId, Guid LeafId)> CreateThreeLevelChainAsync()
	{
		var top = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"top.{Guid.NewGuid():N}@t.com"));
		var mid = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"mid.{Guid.NewGuid():N}@t.com"));
		var leaf = await _employeePersonalInfo.CreateAsync(await BuildCreateDtoAsync($"leaf.{Guid.NewGuid():N}@t.com"));
		await _reportingManager.AssignReportingManagerAsync(mid.Id, new AssignReportingManagerDto { ReportingManagerId = top.Id });
		await _reportingManager.AssignReportingManagerAsync(leaf.Id, new AssignReportingManagerDto { ReportingManagerId = mid.Id });
		return (top.Id, mid.Id, leaf.Id);
	}

	private async Task<CreateEmployeePersonalInfoDto> BuildCreateDtoAsync(
		string email,
		EmployeeStatusType employeeStatusType = EmployeeStatusType.Active)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var organizationId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		return new CreateEmployeePersonalInfoDto
		{
			FirstName = "Test",
			LastName = "Employee",
			MobileNo = "9876543210",
			Email = email,
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = employeeStatusType,
			RoleId = roleId,
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		};
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		var existing = roles.FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var id = _guidGenerator.Create();
		var role = new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id);
		await _roleRepository.InsertAsync(role, autoSave: true);
		return id;
	}

	private async Task<Guid> EnsureShiftExistsAsync()
	{
		var shifts = await _shiftRepository.GetListAsync();
		var existing = shifts.FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = "Test Shift",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(16, 0)
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}
}
