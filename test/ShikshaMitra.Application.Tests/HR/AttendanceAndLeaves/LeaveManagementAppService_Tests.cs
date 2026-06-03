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
using ShikshaMitra.HR.Employees.ReportingManager;
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
	private readonly IReportingManagerAppService _reportingManagerAppService;
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
		_reportingManagerAppService = GetRequiredService<IReportingManagerAppService>();
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

		await WithUnitOfWorkAsync(async () =>
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
		});

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

		await WithUnitOfWorkAsync(async () =>
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
		});

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

		await WithUnitOfWorkAsync(async () =>
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
		});

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetCalendarAsync(BuildCalendarInput(
				shortAyId,
				new DateTimeOffset(new DateTime(now.Year, 6, 1, 0, 0, 0), TimeSpan.FromHours(-7)),
				new DateTimeOffset(new DateTime(now.Year, 6, 30, 23, 59, 59), TimeSpan.FromHours(-7)))));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
	}

	[Fact]
	public async Task CreateMyAsync_Should_Reject_When_Notice_Period_Is_Not_Met()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000024");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var employee = await CreateLinkedEmployeeAsync("notice.reject");
		var policy = await CreateLeavePolicyAsync("NoticeReject", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(employee.UserId!.Value)))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveManagement.CreateMyAsync(
					academicYearId,
					new LeaveRequestCreateDto
					{
						LeavePolicyId = policy.Id,
						StartDate = shortNoticeDate,
						EndDate = shortNoticeDate,
						Reason = "Short notice leave request"
					}));

			ex.Code.ShouldBe(HRErrorCodes.LeaveNoticePeriod);
		}
	}

	[Fact]
	public async Task CreateMyAsync_Should_Create_When_Notice_Period_Is_Met_And_Preserve_HalfDay()
	{
		var validLeaveDate = GetBusinessDateAtOrAfterMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000025");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), validLeaveDate.AddDays(5));
		var employee = await CreateLinkedEmployeeAsync("notice.accept");
		var policy = await CreateLeavePolicyAsync("NoticeAccept", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(employee.UserId!.Value)))
		{
			var result = await _leaveManagement.CreateMyAsync(
				academicYearId,
				new LeaveRequestCreateDto
				{
					LeavePolicyId = policy.Id,
					StartDate = validLeaveDate,
					EndDate = validLeaveDate,
					HalfDay = true,
					Reason = "Valid half-day notice request"
				});

			var persisted = await GetPersistedLeaveAsync(result.Id);

			result.Id.ShouldNotBe(Guid.Empty);
			result.PublicId.ShouldBeGreaterThan(0);
			result.Status.ShouldBe("PENDING");
			persisted.HalfDay.ShouldBeTrue();
			persisted.EmployeeId.ShouldBe(employee.Id);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Admin_Self_Create_When_Notice_Period_Is_Not_Met()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000026");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var admin = await CreateLinkedEmployeeAsync("admin.self.notice");
		var policy = await CreateLeavePolicyAsync("AdminSelfNotice", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(admin.UserId!.Value, roles: ["admin"])))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveManagement.CreateAsync(
					academicYearId,
					new LeaveRequestCreateDto
					{
						LeavePolicyId = policy.Id,
						StartDate = shortNoticeDate,
						EndDate = shortNoticeDate,
						Reason = "Admin self short-notice leave request"
					}));

			ex.Code.ShouldBe(HRErrorCodes.LeaveNoticePeriod);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Admin_Explicit_Self_Target_When_Notice_Period_Is_Not_Met()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000027");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var admin = await CreateLinkedEmployeeAsync("admin.explicit.self.notice");
		var policy = await CreateLeavePolicyAsync("AdminExplicitSelfNotice", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(admin.UserId!.Value, roles: ["admin"])))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveManagement.CreateAsync(
					academicYearId,
					new LeaveRequestCreateDto
					{
						ForEmployeeId = admin.Id,
						LeavePolicyId = policy.Id,
						StartDate = shortNoticeDate,
						EndDate = shortNoticeDate,
						Reason = "Admin explicit self short-notice leave request"
					}));

			ex.Code.ShouldBe(HRErrorCodes.LeaveNoticePeriod);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Allow_Admin_OnBehalf_Past_Leave()
	{
		var pastLeaveDate = GetPastBusinessDate();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000028");
		await SeedAcademicYearAsync(academicYearId, pastLeaveDate.AddDays(-5), DateTime.Today.AddDays(30));
		var employee = await CreateLinkedEmployeeAsync("admin.past");
		var policy = await CreateLeavePolicyAsync("AdminPast", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), roles: ["admin"])))
		{
			var result = await _leaveManagement.CreateAsync(
				academicYearId,
				new LeaveRequestCreateDto
				{
					ForEmployeeId = employee.Id,
					LeavePolicyId = policy.Id,
					StartDate = pastLeaveDate,
					EndDate = pastLeaveDate,
					Reason = "Admin past leave request"
				});

			var persisted = await GetPersistedLeaveAsync(result.Id);

			result.Id.ShouldNotBe(Guid.Empty);
			persisted.EmployeeId.ShouldBe(employee.Id);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Allow_Linked_Admin_OnBehalf_Before_Notice_Period()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000029");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var admin = await CreateLinkedEmployeeAsync("linked.admin.notice");
		var employee = await CreateLinkedEmployeeAsync("linked.admin.target.notice");
		var policy = await CreateLeavePolicyAsync("LinkedAdminNotice", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(admin.UserId!.Value, roles: ["admin"])))
		{
			var result = await _leaveManagement.CreateAsync(
				academicYearId,
				new LeaveRequestCreateDto
				{
					ForEmployeeId = employee.Id,
					LeavePolicyId = policy.Id,
					StartDate = shortNoticeDate,
					EndDate = shortNoticeDate,
					Reason = "Linked admin short-notice on-behalf leave request"
				});

			var persisted = await GetPersistedLeaveAsync(result.Id);

			result.Id.ShouldNotBe(Guid.Empty);
			persisted.EmployeeId.ShouldBe(employee.Id);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Allow_Reporting_Manager_OnBehalf_Before_Notice_Period()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000030");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var manager = await CreateLinkedEmployeeAsync("manager.notice");
		var employee = await CreateLinkedEmployeeAsync("reportee.notice");
		var policy = await CreateLeavePolicyAsync("ManagerNotice", academicYearId);
		await _reportingManagerAppService.AssignReportingManagerAsync(
			employee.Id,
			new AssignReportingManagerDto { ReportingManagerId = manager.Id });

		using (_currentPrincipalAccessor.Change(BuildPrincipal(manager.UserId!.Value)))
		{
			var result = await _leaveManagement.CreateAsync(
				academicYearId,
				new LeaveRequestCreateDto
				{
					ForEmployeeId = employee.Id,
					LeavePolicyId = policy.Id,
					StartDate = shortNoticeDate,
					EndDate = shortNoticeDate,
					Reason = "Manager short-notice leave request"
				});

			var persisted = await GetPersistedLeaveAsync(result.Id);

			result.Id.ShouldNotBe(Guid.Empty);
			persisted.EmployeeId.ShouldBe(employee.Id);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unrelated_Employee_OnBehalf_Request()
	{
		var shortNoticeDate = GetFutureBusinessDateBeforeMinimumNotice();
		var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000031");
		await SeedAcademicYearAsync(academicYearId, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(30));
		var actor = await CreateLinkedEmployeeAsync("actor.unrelated");
		var employee = await CreateLinkedEmployeeAsync("target.unrelated");
		var policy = await CreateLeavePolicyAsync("UnrelatedTarget", academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(actor.UserId!.Value)))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await _leaveManagement.CreateAsync(
					academicYearId,
					new LeaveRequestCreateDto
					{
						ForEmployeeId = employee.Id,
						LeavePolicyId = policy.Id,
						StartDate = shortNoticeDate,
						EndDate = shortNoticeDate,
						Reason = "Unrelated on-behalf leave request"
					}));

			ex.Code.ShouldBe(HRErrorCodes.LeaveApproverNotInReportingChain);
		}
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
		var organizationId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var uniq = Guid.NewGuid().ToString("N")[..8];

		return await _personalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"{prefix}-{uniq}",
			LastName = "Employee",
			MobileNo = $"9{DateTime.UtcNow.Ticks % 1000000000:D9}",
			Email = email ?? $"{prefix}.{uniq}@test.local",
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		});
	}

	private async Task<LeavePolicyResponse> CreateLeavePolicyAsync(string prefix, Guid? academicYearId = null)
	{
		var uniq = Guid.NewGuid().ToString("N")[..8];
		var targetAcademicYearId = academicYearId ?? TestAcademicYearId;
		var leaveType = await _leaveTypeAppService.CreateLeaveTypeAsync(
			targetAcademicYearId,
			new LeaveTypeCreateRequest
			{
				Name = $"{prefix} Type {uniq}",
				Color = "#4CAF50"
			});

		return await _leavePolicyAppService.CreateLeavePolicyAsync(
			targetAcademicYearId,
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

	private async Task SeedAcademicYearAsync(Guid academicYearId, DateTime startDate, DateTime endDate)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var now = DateTime.UtcNow;
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				academicYearId,
				$"Notice Test Year {academicYearId:N}",
				startDate.Date,
				endDate.Date,
				Guid.NewGuid().ToString(),
				"{}",
				now);
		});
	}

	private async Task<EmployeeLeave> GetPersistedLeaveAsync(Guid leaveId)
	{
		var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
		return await dbContext.Set<EmployeeLeave>()
			.IgnoreQueryFilters()
			.FirstAsync(x => x.Id == leaveId);
	}

	private static DateTime GetFutureBusinessDateBeforeMinimumNotice()
	{
		var candidate = DateTime.Today.AddDays(1);
		if (candidate.DayOfWeek == DayOfWeek.Sunday)
		{
			candidate = DateTime.Today;
		}

		return candidate;
	}

	private static DateTime GetBusinessDateAtOrAfterMinimumNotice()
	{
		var candidate = DateTime.Today.AddDays(2);
		while (candidate.DayOfWeek == DayOfWeek.Sunday)
		{
			candidate = candidate.AddDays(1);
		}

		return candidate;
	}

	private static DateTime GetPastBusinessDate()
	{
		var candidate = DateTime.Today.AddDays(-1);
		while (candidate.DayOfWeek == DayOfWeek.Sunday)
		{
			candidate = candidate.AddDays(-1);
		}

		return candidate;
	}

	private ClaimsPrincipal BuildPrincipal(
		Guid userId,
		string? userName = null,
		string? email = null,
		string[]? roles = null)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, userName ?? $"user-{userId:N}"),
			new(AbpClaimTypes.Email, email ?? $"user-{userId:N}@test.local"),
			new(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		};
		foreach (var role in roles ?? [])
		{
			claims.Add(new Claim(AbpClaimTypes.Role, role));
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

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
