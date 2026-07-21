using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<int> EnsurePolicyAssignmentAsync(
		AcademicYear academicYear,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		string targetDisplayName,
		PayrollPolicy payrollPolicy,
		AttendancePolicy attendancePolicy,
		LeavePolicy leavePolicy,
		int priority,
		string reason)
	{
		var normalizedTargetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, targetKey);
		var effectiveFrom = academicYear.StartDate.Date;
		var effectiveTo = academicYear.EndDate.Date;
		var existing = await _hrPolicyAssignmentRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.TargetType == targetType &&
			x.TargetKey == normalizedTargetKey &&
			x.EffectiveFrom == effectiveFrom &&
			x.EffectiveTo == effectiveTo);
		if (existing != null)
		{
			var needsUpdate =
				existing.AttendancePolicyId != attendancePolicy.Id ||
				existing.PayrollPolicyId != payrollPolicy.Id ||
				existing.LeavePolicyId != leavePolicy.Id ||
				existing.Priority != priority ||
				!existing.IsActive ||
				!string.Equals(existing.AssignmentReason, reason, StringComparison.Ordinal) ||
				!string.Equals(existing.TargetDisplayName, targetDisplayName, StringComparison.Ordinal);
			if (needsUpdate)
			{
				existing.UpdateTarget(targetType, normalizedTargetKey, targetDisplayName);
				existing.Update(
					attendancePolicy.Id,
					payrollPolicy.Id,
					leavePolicy.Id,
					effectiveFrom,
					effectiveTo,
					true,
					priority,
					reason);
				await _hrPolicyAssignmentRepository.UpdateAsync(existing, autoSave: false);
			}

			return 0;
		}

		var assignment = new HrPolicyAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			targetType,
			normalizedTargetKey,
			targetDisplayName,
			attendancePolicy.Id,
			payrollPolicy.Id,
			leavePolicy.Id,
			effectiveFrom,
			effectiveTo,
			true,
			priority,
			reason);
		await _hrPolicyAssignmentRepository.InsertAsync(assignment, autoSave: false);
		return 1;
	}
}
