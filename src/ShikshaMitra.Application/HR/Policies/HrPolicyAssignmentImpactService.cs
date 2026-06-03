using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentImpactService(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyAssignmentTargetValidator targetValidator,
	HrPolicyAssignmentEmployeePreviewResolver employeePreviewResolver,
	HrPolicyEffectiveAssignmentConflictBuilder effectiveConflictBuilder,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input)
	{
		var assignment = input.Assignment;
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(assignment.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, assignment.TargetKey);
		await targetValidator.ValidateReferencesAsync(assignment);
		await targetValidator.ValidateTargetAsync(targetType, targetKey);

		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = "assignment",
			PolicyId = input.AssignmentId,
			AcademicYearId = assignment.AcademicYearId,
			Action = input.Action,
			ProposedVersion = input.AssignmentId.HasValue ? 2 : 1
		};

		preview.AffectedEmployees = await employeePreviewResolver.ResolveAsync(targetType, targetKey);
		preview.AffectedEmployeeCount = preview.AffectedEmployees.Count;
		preview.AffectedAssignmentCount = 1;
		preview.EffectiveAssignmentConflicts = await effectiveConflictBuilder.BuildAsync(
			targetType,
			targetKey,
			assignment,
			preview.AffectedEmployees);

		try
		{
			await EnsureNoOverlapAsync(targetType, targetKey, assignment.AcademicYearId, assignment.EffectiveFrom, assignment.EffectiveTo, input.AssignmentId, assignment.IsActive);
		}
		catch (BusinessException ex) when (ex.Code == HRErrorCodes.HrPolicyAssignmentOverlap)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "blocking",
				Code = ex.Code,
				Message = "This assignment overlaps an existing active assignment for the same target and date range."
			});
		}

		if (preview.AffectedEmployeeCount == 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "warning",
				Code = HRErrorCodes.HrPolicyNoAffectedEmployees,
				Message = "No active employees currently match this assignment target."
			});
		}

		await confirmationService.AttachTokenAsync(preview);
		return preview;
	}

	public async Task EnsureAssignmentImpactConfirmedAsync(string action, Guid? assignmentId, CreateUpdateHrPolicyAssignmentDto input)
	{
		var preview = await PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = action,
			AssignmentId = assignmentId,
			Assignment = input
		});
		await confirmationService.EnsureConfirmedAsync(preview, input.ConfirmationToken);
	}

	public Task ValidateReferencesAsync(CreateUpdateHrPolicyAssignmentDto input) =>
		targetValidator.ValidateReferencesAsync(input);

	public Task ValidateTargetAsync(HrPolicyAssignmentTargetType targetType, string targetKey) =>
		targetValidator.ValidateTargetAsync(targetType, targetKey);

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
			.ThenBy(x => x.EffectiveFrom));

		if (overlap != null)
		{
			var payroll = await payrollPolicyRepository.FindAsync(overlap.PayrollPolicyId);
			var attendance = await attendancePolicyRepository.FindAsync(overlap.AttendancePolicyId);
			var conflict = new HrPolicyAssignmentConflictDto
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
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentOverlap)
				.WithData("conflict", JsonSerializer.Serialize(conflict))
				.WithData("conflictingAssignmentId", overlap.Id)
				.WithData("targetType", conflict.TargetType)
				.WithData("targetKey", conflict.TargetKey);
		}
	}
}
