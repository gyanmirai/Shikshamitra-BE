using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestAcademicYearId = Guid.Parse("00000000-0000-0000-0000-000000000020");

	private readonly ILeaveManagementAppService _leaveManagement;
	private readonly ILeaveTypeAppService _leaveTypeAppService;
	private readonly ILeavePolicyAppService _leavePolicyAppService;
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public LeaveManagementAppService_Tests()
	{
		_leaveManagement = GetRequiredService<ILeaveManagementAppService>();
		_leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
		_leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		SeedAcademicYearForListTests();
	}

	private static LeaveManagementGetListInput BuildListInput(
		Guid academicYearId,
		DateTime monthYearAnchor,
		string? search,
		int page,
		int pageSize)
	{
		return new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = monthYearAnchor,
			Search = search,
			Page = page,
			PageSize = pageSize
		};
	}

	private static LeaveManagementGetListInput BuildListInputFullAcademicYear(
		Guid academicYearId,
		string? search,
		int page,
		int pageSize)
	{
		return new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = null,
			Search = search,
			Page = page,
			PageSize = pageSize
		};
	}

	private static LeaveManagementCalendarInput BuildCalendarInput(
		Guid academicYearId,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		int page = 1,
		int pageSize = 100)
	{
		return new LeaveManagementCalendarInput
		{
			AcademicYearId = academicYearId,
			RangeStart = rangeStart,
			RangeEnd = rangeEnd,
			Page = page,
			PageSize = pageSize
		};
	}

	private void SeedAcademicYearForListTests()
	{
		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var now = DateTime.UtcNow;
			var stamp = Guid.NewGuid().ToString();
			var start = new DateTime(now.Year, 1, 1);
			var end = new DateTime(now.Year, 12, 31);

			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				TestAcademicYearId,
				"LeaveMgmt Test Year",
				start,
				end,
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Empty_When_No_Leaves()
	{
		var anchor = new DateTime(DateTime.UtcNow.Year, 6, 15);
		var result = await _leaveManagement.GetListAsync(BuildListInput(TestAcademicYearId, anchor, null, 1, 10));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Empty_When_No_Leaves_Full_Academic_Year()
	{
		var result = await _leaveManagement.GetListAsync(BuildListInputFullAcademicYear(TestAcademicYearId, null, 1, 10));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetMyBalancesAsync_Should_Return_Balances_For_Linked_Employee_User()
	{
		var employee = await CreateLinkedEmployeeAsync("self.balance");
		var policy = await CreateLeavePolicyAsync("SelfBalance");

		using (_currentPrincipalAccessor.Change(BuildPrincipal(employee.UserId!.Value)))
		{
			var result = await _leaveManagement.GetMyBalancesAsync(TestAcademicYearId);

			result.Items.ShouldContain(x => x.LeavePolicyId == policy.Id);
		}
	}

	[Fact]
	public async Task GetMyBalancesAsync_Should_Link_Seeded_Principal_Alias_When_UserId_Is_Missing_Or_Stale()
	{
		var employee = await CreateLinkedEmployeeAsync(
			"seeded.principal",
			email: "hiren.vadher@ddvs.com");
		var principalUserId = Guid.NewGuid();
		var policy = await CreateLeavePolicyAsync("SeededPrincipalAlias");

		using (_currentPrincipalAccessor.Change(BuildPrincipal(
			       principalUserId,
			       userName: "principal-gseb",
			       email: "principal-gseb@ddvs.com")))
		{
			var result = await _leaveManagement.GetMyBalancesAsync(TestAcademicYearId);

			result.Items.ShouldContain(x => x.LeavePolicyId == policy.Id);
		}
	}

	[Fact]
	public async Task GetMyBalancesAsync_Should_Throw_When_User_Is_Not_Linked_To_Employee()
	{
		using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid())))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveManagement.GetMyBalancesAsync(TestAcademicYearId));

			ex.Code.ShouldBe(HRErrorCodes.LeaveEmployeeNotFound);
		}
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_Invalid_Page()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 0, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPage);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_PageSize_Below_Minimum()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 5)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_PageSize_Above_Maximum()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 200)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_Invalid_Year_Anchor()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(1800, 6, 1), null, 1, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidYear);
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_Academic_Year_Missing()
	{
		await Should.ThrowAsync<EntityNotFoundException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(Guid.NewGuid(), new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 10)));
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_Month_Outside_Academic_Year()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000021");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Short AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(shortAyId, new DateTime(now.Year, 6, 1), null, 1, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
	}

	[Fact]
	public async Task GetCalendarAsync_Should_Clip_Offset_Range_To_AcademicYear()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000022");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Calendar Offset AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var result = await _leaveManagement.GetCalendarAsync(BuildCalendarInput(
			shortAyId,
			new DateTimeOffset(new DateTime(now.Year, 2, 20, 0, 0, 0), TimeSpan.FromHours(-7)),
			new DateTimeOffset(new DateTime(now.Year, 4, 5, 23, 59, 59), TimeSpan.FromHours(-7))));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetCalendarAsync_Should_Throw_When_Offset_Range_Is_Completely_Outside_AcademicYear()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000023");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Calendar Outside Offset AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetCalendarAsync(BuildCalendarInput(
				shortAyId,
				new DateTimeOffset(new DateTime(now.Year, 6, 1, 0, 0, 0), TimeSpan.FromHours(-7)),
				new DateTimeOffset(new DateTime(now.Year, 6, 30, 23, 59, 59), TimeSpan.FromHours(-7)))));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
	}

	[Fact]
	public void IsValidStatusTransition_Cancelled_Should_Be_Valid_From_Pending()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.Status.ShouldBe(EmployeeLeaveStatusType.Pending);
		leave.Cancel();
		leave.Status.ShouldBe(EmployeeLeaveStatusType.Cancelled);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Approved()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Rejected()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Rejected, "reason");

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Already_Cancelled()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.Cancel();

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	private async Task<EmployeePersonalInfoDto> CreateLinkedEmployeeAsync(string prefix, string? email = null)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var uniq = Guid.NewGuid().ToString("N")[..8];

		return await _personalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"{prefix}-{uniq}",
			LastName = "Employee",
			MobileNo = $"9{DateTime.UtcNow.Ticks % 1000000000:D9}",
			Email = email ?? $"{prefix}.{uniq}@test.local",
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId
		});
	}

	private async Task<LeavePolicyResponse> CreateLeavePolicyAsync(string prefix)
	{
		var uniq = Guid.NewGuid().ToString("N")[..8];
		var leaveType = await _leaveTypeAppService.CreateLeaveTypeAsync(
			TestAcademicYearId,
			new LeaveTypeCreateRequest
			{
				Name = $"{prefix} Type {uniq}",
				Color = "#4CAF50"
			});

		return await _leavePolicyAppService.CreateLeavePolicyAsync(
			TestAcademicYearId,
			new LeavePolicyCreateRequest
			{
				PolicyTitle = $"{prefix} Policy {uniq}",
				LeaveType = leaveType.Id,
				AccrualPolicyType = "Yearly",
				LeaveCount = 12,
				PaidType = "Paid",
				CarryForward = false,
				EncashmentAllowed = false,
				RequiresApproval = true,
				Description = "Self-service leave test policy"
			});
	}

	private ClaimsPrincipal BuildPrincipal(
		Guid userId,
		string? userName = null,
		string? email = null)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, userName ?? $"user-{userId:N}"),
			new(AbpClaimTypes.Email, email ?? $"user-{userId:N}@test.local"),
			new(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
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

		var id = _guidGenerator.Create();
		var shift = new ShiftManagement
		{
			ShiftName = $"Test Shift {id:N}",
			StartTime = new TimeOnly(8, 0, 0),
			EndTime = new TimeOnly(15, 0, 0)
		};
		typeof(ShiftManagement).GetProperty(nameof(ShiftManagement.Id))!.SetValue(shift, id);
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return id;
	}
}
