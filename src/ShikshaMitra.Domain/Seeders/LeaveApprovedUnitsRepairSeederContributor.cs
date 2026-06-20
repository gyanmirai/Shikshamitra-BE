using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Idempotent repair pass for approved leave payroll snapshots after leave
/// units were aligned with payroll work-calendar holiday counting.
/// </summary>
public class LeaveApprovedUnitsRepairSeederContributor : BaseSeederContributor
{
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly LeaveWorkCalendarUnitService _leaveWorkCalendarUnitService;
	private readonly ILogger<LeaveApprovedUnitsRepairSeederContributor> _logger;

	public LeaveApprovedUnitsRepairSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		IEmployeeLeaveRepository employeeLeaveRepository,
		ILeavePolicyRepository leavePolicyRepository,
		LeaveWorkCalendarUnitService leaveWorkCalendarUnitService,
		ILogger<LeaveApprovedUnitsRepairSeederContributor> logger)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_leaveWorkCalendarUnitService = leaveWorkCalendarUnitService;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var leaves = await _employeeLeaveRepository.GetListAsync(
			x => x.Status == EmployeeLeaveStatusType.Approved);
		if (leaves.Count == 0)
		{
			_logger.LogInformation(
				"[LeaveApprovedUnitsRepairSeeder] No approved leaves found. TenantId={TenantId}",
				CurrentTenant.Id);
			return;
		}

		var academicYearByPolicyId = await GetAcademicYearByPolicyIdAsync(
			leaves.Select(x => x.LeavePolicyId).Distinct().ToList());
		var repaired = 0;

		foreach (var leave in leaves)
		{
			var academicYearId = academicYearByPolicyId.TryGetValue(
				leave.LeavePolicyId,
				out var resolvedAcademicYearId)
				? resolvedAcademicYearId
				: (Guid?)null;
			var correctedUnits = await _leaveWorkCalendarUnitService.CalculateUnitsAsync(
				leave.EmployeeId,
				leave.StartDate.DateTime,
				leave.EndDate.DateTime,
				leave.HalfDay,
				academicYearId);

			var approvedPaidType = leave.ApprovedPaidType ?? leave.PaidType;
			if (leave.ApprovedPaidType == approvedPaidType &&
			    leave.ApprovedLeaveUnits == correctedUnits)
			{
				continue;
			}

			leave.SetApprovalPayrollSnapshot(approvedPaidType, correctedUnits);
			await _employeeLeaveRepository.UpdateAsync(leave, autoSave: false);
			repaired++;
		}

		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[LeaveApprovedUnitsRepairSeeder] Repair completed. TenantId={TenantId}, ApprovedLeaves={ApprovedLeaves}, Repaired={Repaired}",
			CurrentTenant.Id,
			leaves.Count,
			repaired);
	}

	private async Task<Dictionary<Guid, Guid>> GetAcademicYearByPolicyIdAsync(IReadOnlyCollection<Guid> policyIds)
	{
		var result = new Dictionary<Guid, Guid>();
		foreach (var policyId in policyIds)
		{
			var policy = await _leavePolicyRepository.FindWithoutDetailsAsync(policyId);
			if (policy != null)
			{
				result[policyId] = policy.AcademicYearId;
			}
		}

		return result;
	}
}
