using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Services;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollApprovedLeaveResolver_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly PayrollApprovedLeaveResolver _resolver;
	private readonly IEmployeeLeaveRepository _leaveRepository;
	private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public PayrollApprovedLeaveResolver_Tests()
	{
		_resolver = GetRequiredService<PayrollApprovedLeaveResolver>();
		_leaveRepository = GetRequiredService<IEmployeeLeaveRepository>();
		_leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
		_leavePolicyRepository = GetRequiredService<IRepository<LeavePolicy, Guid>>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Repository_Should_Return_Only_Approved_Overlapping_Payroll_Leave_Rows()
	{
		var scenario = await SeedPayrollLeaveScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var rows = await _leaveRepository.GetApprovedPayrollLeaveRowsAsync(
				new[] { scenario.TargetEmployeeId },
				new DateTimeOffset(new DateTime(2026, 1, 1)),
				new DateTimeOffset(new DateTime(2026, 1, 31, 23, 59, 59)));

			rows.Count.ShouldBe(2);
			rows.ShouldContain(x => x.EmployeeId == scenario.TargetEmployeeId && x.StartDate.Date == new DateTime(2026, 1, 5));
			rows.ShouldContain(x => x.EmployeeId == scenario.TargetEmployeeId && x.StartDate.Date == new DateTime(2026, 1, 7));
			rows.ShouldAllBe(x => x.EmployeeId == scenario.TargetEmployeeId);
		}
	}

	[Fact]
	public async Task Resolver_Should_Expand_Only_Filtered_Approved_Overlapping_Leaves()
	{
		var scenario = await SeedPayrollLeaveScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var result = await _resolver.ResolveLeaveUnitsByEmployeeAsync(
				new[] { scenario.TargetEmployeeId },
				2026,
				1);

			var map = result[scenario.TargetEmployeeId];

			map[new DateOnly(2026, 1, 5)].PaidUnits.ShouldBe(1m);
			map[new DateOnly(2026, 1, 6)].PaidUnits.ShouldBe(1m);
			map[new DateOnly(2026, 1, 7)].UnpaidUnits.ShouldBe(0.5m);
			map.ContainsKey(new DateOnly(2026, 1, 8)).ShouldBeFalse();
			map.ContainsKey(new DateOnly(2026, 1, 9)).ShouldBeFalse();
			map.ContainsKey(new DateOnly(2026, 2, 2)).ShouldBeFalse();
		}
	}

	private async Task<PayrollLeaveScenario> SeedPayrollLeaveScenarioAsync()
	{
		var tenantId = _guidGenerator.Create();
		var academicYearId = _guidGenerator.Create();

		using (_currentTenant.Change(tenantId))
		{
			return await WithUnitOfWorkAsync(async () =>
			{
				await SeedAcademicYearAsync(academicYearId);
				var targetEmployee = await CreateEmployeeAsync(tenantId);
				var otherEmployee = await CreateEmployeeAsync(tenantId);
				var paidPolicy = await CreateLeavePolicyAsync(tenantId, academicYearId, LeavePaidType.Paid);
				var unpaidPolicy = await CreateLeavePolicyAsync(tenantId, academicYearId, LeavePaidType.Unpaid);

				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 5)),
					new DateTimeOffset(new DateTime(2026, 1, 6, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					unpaidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 7)),
					new DateTimeOffset(new DateTime(2026, 1, 7, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved,
					halfDay: true,
					approvedPaidType: LeavePaidType.Unpaid);
				await InsertLeaveAsync(
					tenantId,
					otherEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 8)),
					new DateTimeOffset(new DateTime(2026, 1, 8, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 2, 2)),
					new DateTimeOffset(new DateTime(2026, 2, 2, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 9)),
					new DateTimeOffset(new DateTime(2026, 1, 9, 23, 59, 59)),
					EmployeeLeaveStatusType.Pending);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 12)),
					new DateTimeOffset(new DateTime(2026, 1, 12, 23, 59, 59)),
					EmployeeLeaveStatusType.Reviewed);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy.Id,
					new DateTimeOffset(new DateTime(2026, 1, 13)),
					new DateTimeOffset(new DateTime(2026, 1, 13, 23, 59, 59)),
					EmployeeLeaveStatusType.Cancelled);

				return new PayrollLeaveScenario(tenantId, targetEmployee.Id, otherEmployee.Id);
			});
		}
	}

	private async Task SeedAcademicYearAsync(Guid academicYearId)
	{
		var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
		var now = DateTime.UtcNow;
		await dbContext.Database.ExecuteSqlRawAsync(
			"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
			academicYearId,
			$"Payroll Leave Test Year {academicYearId:N}",
			new DateTime(2026, 1, 1),
			new DateTime(2026, 12, 31),
			_guidGenerator.Create().ToString(),
			"{}",
			now);
	}

	private async Task<Employee> CreateEmployeeAsync(Guid tenantId)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var roleId = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(roleId, $"payroll-leave-{suffix[..8]}", tenantId), autoSave: true);

		var employee = new Employee(
			_guidGenerator.Create(),
			tenantId,
			userId: null,
			profilePictureBlobName: null,
			employeeCode: $"PL{suffix}",
			lastName: "Leave",
			firstName: $"Payroll{suffix[..8]}",
			middleName: null,
			shortName: "PL",
			employeeStatusType: EmployeeStatusType.Active,
			dateOfBirth: new DateOnly(1990, 1, 1),
			familyCast: null,
			castType: null,
			genderType: null,
			religion: null,
			maritalStatus: null,
			bloodGroup: null,
			mobileNo: null,
			secondaryMobileNo: null,
			email: $"payrollleave{suffix[..8]}@test.local",
			panNo: UniquePan(),
			adharNo: UniqueAdhar(),
			address1: null,
			address2: null,
			address3: null,
			department: null,
			designation: null,
			dateOfJoining: new DateOnly(2025, 1, 1),
			employeeGroup: null,
			qualification: null,
			roleId: roleId,
			identificationMark1: null,
			city: null,
			state: null,
			country: null,
			pinCode: null,
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: null,
			shiftId: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private async Task<LeavePolicy> CreateLeavePolicyAsync(Guid tenantId, Guid academicYearId, LeavePaidType paidType)
	{
		var suffix = _guidGenerator.Create().ToString("N")[..8];
		var leaveType = new LeaveType(
			_guidGenerator.Create(),
			tenantId,
			academicYearId,
			$"{paidType} Payroll Leave {suffix}",
			"#4CAF50");
		await _leaveTypeRepository.InsertAsync(leaveType, autoSave: true);

		var policy = new LeavePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYearId,
			$"{paidType} Payroll Policy {suffix}",
			leaveType.Id,
			LeaveAccrualPolicyType.Yearly,
			12,
			paidType,
			carryForward: false,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true);

		return await _leavePolicyRepository.InsertAsync(policy, autoSave: true);
	}

	private async Task InsertLeaveAsync(
		Guid tenantId,
		Guid employeeId,
		Guid policyId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		EmployeeLeaveStatusType status,
		bool halfDay = false,
		LeavePaidType? approvedPaidType = null)
	{
		var leave = EmployeeLeave.Create(
			_guidGenerator.Create(),
			tenantId,
			employeeId,
			policyId,
			startDate,
			endDate,
			"Payroll approved leave resolver test",
			halfDay);

		if (approvedPaidType.HasValue)
		{
			leave.SetApprovalPayrollSnapshot(approvedPaidType.Value, halfDay ? 0.5m : 1m);
		}

		leave.UpdateLeaveStatus(status, status == EmployeeLeaveStatusType.Rejected ? "Rejected" : null);
		await _leaveRepository.InsertAsync(leave, autoSave: true);
	}

	private sealed record PayrollLeaveScenario(Guid TenantId, Guid TargetEmployeeId, Guid OtherEmployeeId);
}
