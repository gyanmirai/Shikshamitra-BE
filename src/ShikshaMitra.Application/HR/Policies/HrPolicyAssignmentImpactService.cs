using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentImpactService(
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyAssignmentTargetValidator targetValidator,
	HrPolicyAssignmentEmployeePreviewResolver employeePreviewResolver,
	HrPolicyEffectiveAssignmentConflictBuilder effectiveConflictBuilder,
	HrPolicyAssignmentReferenceWarningService referenceWarningService,
	HrPolicyAssignmentOverlapChecker overlapChecker,
	HrPolicyEmployeeOverrideImpactCounter employeeOverrideImpactCounter,
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public const string EmployeeOverrideUpsertAction = "employee-override-upsert";
	public const string EmployeeOverrideDisableAction = "employee-override-disable";

	public async Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input)
	{
		var assignment = input.Assignment;
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(assignment.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, assignment.TargetKey);
		var action = NormalizeAction(input.Action);
		var assignmentId = await ResolvePreviewAssignmentIdAsync(input.AssignmentId, action, targetType, targetKey, assignment);
		var isEmployeeOverrideDisable = string.Equals(action, EmployeeOverrideDisableAction, StringComparison.OrdinalIgnoreCase);
		var isDelete = string.Equals(action, "delete", StringComparison.OrdinalIgnoreCase) || isEmployeeOverrideDisable;
		if (!isDelete)
		{
			await targetValidator.ValidateTargetAsync(targetType, targetKey, assignment.AcademicYearId);
		}

		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = "assignment",
			PolicyId = assignmentId,
			AcademicYearId = assignment.AcademicYearId,
			Action = action,
			ProposedVersion = assignmentId.HasValue ? 2 : 1
		};

		var referencesAreUsable = isDelete ||
			await referenceWarningService.AddReferenceWarningsAsync(preview, assignment);
		var affectedEmployees = await ResolveAffectedEmployeesAsync(targetType, targetKey, isDelete);
		preview.AffectedEmployeeCount = affectedEmployees.Count;
		preview.AffectedEmployees = affectedEmployees.Sample
			.Select(HrPolicyAssignmentProjection.MapAffectedEmployee)
			.ToList();
		if (!isDelete && referencesAreUsable)
		{
			preview.EffectiveAssignmentConflicts = await effectiveConflictBuilder.BuildAsync(
				targetType,
				targetKey,
				assignment,
				preview.AffectedEmployees,
				assignmentId);
		}

		preview.AffectedAssignmentCount = isEmployeeOverrideDisable
			? await employeeOverrideImpactCounter.CountActiveAsync(assignment, targetType, targetKey)
			: 1;

		if (!isDelete)
		{
			await AddOverlapWarningAsync(preview, action, targetType, targetKey, assignment, assignmentId);
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
		else
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "info",
				Code = HRErrorCodes.AffectedEmployees,
				Message = $"{preview.AffectedEmployeeCount} employees may be affected."
			});
		}

		await confirmationService.AttachTokenAsync(preview);
		HrPolicyImpactWarningBuilder.FinalizeSummary(preview);
		return preview;
	}

	public async Task EnsureAssignmentImpactConfirmedAsync(
		string action,
		Guid? assignmentId,
		CreateUpdateHrPolicyAssignmentDto input)
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

	public Task ValidateTargetAsync(HrPolicyAssignmentTargetType targetType, string targetKey, Guid academicYearId) =>
		targetValidator.ValidateTargetAsync(targetType, targetKey, academicYearId);

	public Task EnsureNoOverlapAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		Guid academicYearId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		Guid? excludeId,
		bool isActive) =>
		overlapChecker.EnsureNoOverlapAsync(targetType, targetKey, academicYearId, effectiveFrom, effectiveTo, excludeId, isActive);

	private async Task AddOverlapWarningAsync(
		HrPolicyImpactPreviewDto preview,
		string action,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto assignment,
		Guid? assignmentId)
	{
		try
		{
			await EnsureNoOverlapAsync(
				targetType,
				targetKey,
				assignment.AcademicYearId,
				assignment.EffectiveFrom,
				assignment.EffectiveTo,
				assignmentId,
				assignment.IsActive);
		}
		catch (BusinessException ex) when (ex.Code == HRErrorCodes.HrPolicyAssignmentOverlap)
		{
			var isConfirmableEmployeeOverrideRepair = targetType == HrPolicyAssignmentTargetType.Employee &&
				assignmentId.HasValue &&
				string.Equals(action, EmployeeOverrideUpsertAction, StringComparison.OrdinalIgnoreCase);
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = isConfirmableEmployeeOverrideRepair ? "warning" : "blocking",
				Code = ex.Code,
				Message = isConfirmableEmployeeOverrideRepair
					? "This employee already has an active override for this date range. Confirming this change will deactivate the competing override."
					: "This assignment overlaps an existing active assignment for the same target and date range."
			});
		}
	}

	private static string NormalizeAction(string? action) =>
		string.IsNullOrWhiteSpace(action) ? "update" : action.Trim().ToLowerInvariant();

	private async Task<Guid?> ResolvePreviewAssignmentIdAsync(
		Guid? assignmentId,
		string action,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto assignment)
	{
		if (assignmentId.HasValue ||
			targetType != HrPolicyAssignmentTargetType.Employee ||
			(!string.Equals(action, EmployeeOverrideUpsertAction, StringComparison.OrdinalIgnoreCase) &&
			 !string.Equals(action, EmployeeOverrideDisableAction, StringComparison.OrdinalIgnoreCase)))
		{
			return assignmentId;
		}

		var effectiveEnd = assignment.EffectiveTo?.Date ?? DateTime.MaxValue.Date;
		var start = assignment.EffectiveFrom.Date;
		var query = await assignmentRepository.GetQueryableAsync();
		var existing = await asyncExecuter.FirstOrDefaultAsync(query
			.Where(x =>
				x.AcademicYearId == assignment.AcademicYearId &&
				x.TargetType == targetType &&
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= effectiveEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= start))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id));

		return existing?.Id;
	}

	private async Task<AffectedEmployeePreview> ResolveAffectedEmployeesAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		bool allowMissingTarget)
	{
		try
		{
			return await employeePreviewResolver.ResolveAsync(targetType, targetKey);
		}
		catch (EntityNotFoundException) when (allowMissingTarget)
		{
			return AffectedEmployeePreview.Empty;
		}
	}
}
