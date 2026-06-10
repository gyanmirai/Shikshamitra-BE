using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentOverlapChecker(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task EnsureNoOverlapAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		Guid academicYearId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		Guid? excludeId,
		bool isActive)
	{
		if (!isActive)
		{
			return;
		}

		var start = effectiveFrom.Date;
		var end = effectiveTo?.Date;
		var effectiveEnd = end ?? DateTime.MaxValue.Date;
		var query = await assignmentRepository.GetQueryableAsync();
		var overlap = await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.AcademicYearId == academicYearId &&
			x.IsActive &&
			(!excludeId.HasValue || x.Id != excludeId.Value) &&
			x.EffectiveFrom <= effectiveEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo >= start))
			.OrderByDescending(x => x.Priority)
			.ThenBy(x => x.EffectiveFrom)
			.ThenBy(x => x.Id));

		if (overlap == null)
		{
			return;
		}

		var conflict = await BuildConflictAsync(overlap);
		throw new BusinessException(HRErrorCodes.HrPolicyAssignmentOverlap)
			.WithData("conflict", JsonSerializer.Serialize(conflict))
			.WithData("conflictingAssignmentId", overlap.Id)
			.WithData("targetType", conflict.TargetType)
			.WithData("targetKey", conflict.TargetKey);
	}

	private async Task<HrPolicyAssignmentConflictDto> BuildConflictAsync(HrPolicyAssignment overlap)
	{
		var payroll = await payrollPolicyRepository.FindAsync(overlap.PayrollPolicyId);
		var attendance = await attendancePolicyRepository.FindAsync(overlap.AttendancePolicyId);
		return new HrPolicyAssignmentConflictDto
		{
			AssignmentId = overlap.Id,
			TargetType = HrPolicyMapping.MapAssignmentTargetType(overlap.TargetType),
			TargetKey = overlap.TargetKey,
			TargetDisplayName = overlap.TargetDisplayName,
			PayrollPolicyId = overlap.PayrollPolicyId,
			PayrollPolicyName = payroll?.Name ?? "Unknown payroll policy",
			AttendancePolicyId = overlap.AttendancePolicyId,
			AttendancePolicyName = attendance?.Name ?? "Unknown attendance policy",
			EffectiveFrom = overlap.EffectiveFrom,
			EffectiveTo = overlap.EffectiveTo,
			Priority = overlap.Priority,
			SuggestedFixes =
			[
				"Adjust the effective dates so the ranges do not overlap.",
				"Deactivate the existing assignment before creating this one.",
				"Edit the existing assignment instead of creating a duplicate range."
			]
		};
	}
}
