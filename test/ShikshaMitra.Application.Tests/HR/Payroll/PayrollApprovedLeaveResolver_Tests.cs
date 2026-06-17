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
using ShikshaMitra.HR.Policies;
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
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly IRepository<LeavePolicyRule, Guid> _leavePolicyRuleRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public PayrollApprovedLeaveResolver_Tests()
	{
		_resolver = GetRequiredService<PayrollApprovedLeaveResolver>();
		_leaveRepository = GetRequiredService<IEmployeeLeaveRepository>();
		_leavePolicyRepository = GetRequiredService<IRepository<LeavePolicy, Guid>>();
		_leavePolicyRuleRepository = GetRequiredService<IRepository<LeavePolicyRule, Guid>>();
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

	[Fact]
	public async Task Resolver_Should_Use_Working_Units_By_Employee_When_Provided()
	{
		var scenario = await SeedPayrollLeaveScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var workingUnitsByEmployee = new Dictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>
			{
				[scenario.TargetEmployeeId] = new Dictionary<DateOnly, decimal>
				{
					[new(2026, 1, 5)] = 1m,
					[new(2026, 1, 6)] = 0m,
					[new(2026, 1, 7)] = 0.5m
				}
			};

			var result = await _resolver.ResolveLeaveUnitsByEmployeeAsync(
				new[] { scenario.TargetEmployeeId },
				2026,
				1,
				workingUnitsByEmployee);

			var map = result[scenario.TargetEmployeeId];

			map[new DateOnly(2026, 1, 5)].PaidUnits.ShouldBe(1m);
			map.ContainsKey(new DateOnly(2026, 1, 6)).ShouldBeFalse();
			map[new DateOnly(2026, 1, 7)].UnpaidUnits.ShouldBe(0.5m);
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
					paidPolicy,
					new DateTimeOffset(new DateTime(2026, 1, 5)),
					new DateTimeOffset(new DateTime(2026, 1, 6, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					unpaidPolicy,
					new DateTimeOffset(new DateTime(2026, 1, 7)),
					new DateTimeOffset(new DateTime(2026, 1, 7, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved,
					halfDay: true,
					approvedPaidType: LeavePaidType.Unpaid);
				await InsertLeaveAsync(
					tenantId,
					otherEmployee.Id,
					paidPolicy,
					new DateTimeOffset(new DateTime(2026, 1, 8)),
					new DateTimeOffset(new DateTime(2026, 1, 8, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy,
					new DateTimeOffset(new DateTime(2026, 2, 2)),
					new DateTimeOffset(new DateTime(2026, 2, 2, 23, 59, 59)),
					EmployeeLeaveStatusType.Approved);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy,
					new DateTimeOffset(new DateTime(2026, 1, 9)),
					new DateTimeOffset(new DateTime(2026, 1, 9, 23, 59, 59)),
					EmployeeLeaveStatusType.Pending);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy,
					new DateTimeOffset(new DateTime(2026, 1, 12)),
					new DateTimeOffset(new DateTime(2026, 1, 12, 23, 59, 59)),
					EmployeeLeaveStatusType.Reviewed);
				await InsertLeaveAsync(
					tenantId,
					targetEmployee.Id,
					paidPolicy,
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
			employeeCode: $"PL{suffix}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Leave",
				FirstName: $"Payroll{suffix[..8]}",
				MiddleName: null,
				ShortName: "PL",
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: new DateOnly(1990, 1, 1),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"payrollleave{suffix[..8]}@test.local",
				PanNo: UniquePan(),
				AdharNo: UniqueAdhar(),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: null,
				Designation: null,
				DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: null,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private async Task<PayrollLeavePolicySetup> CreateLeavePolicyAsync(Guid tenantId, Guid academicYearId, LeavePaidType paidType)
	{
		var suffix = _guidGenerator.Create().ToString("N")[..8];
		var policy = new LeavePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYearId,
			$"{paidType} Payroll Policy {suffix}",
			DateTime.UtcNow.Date,
			null,
			HrPolicyStatus.Active);
		await _leavePolicyRepository.InsertAsync(policy, autoSave: true);

		var rule = new LeavePolicyRule(
			_guidGenerator.Create(),
			tenantId,
			policy.Id,
			paidType,
			LeaveEntitlementMode.Limited,
			LeaveAccrualPolicyType.Yearly,
			12,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance: false,
			isActive: true);
		await _leavePolicyRuleRepository.InsertAsync(rule, autoSave: true);

		return new PayrollLeavePolicySetup(policy.Id, rule.Id, paidType);
	}

	private async Task InsertLeaveAsync(
		Guid tenantId,
		Guid employeeId,
		PayrollLeavePolicySetup policy,
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
			policy.PolicyId,
			policy.RuleId,
			policy.PaidType,
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
	private sealed record PayrollLeavePolicySetup(Guid PolicyId, Guid RuleId, LeavePaidType PaidType);
}
