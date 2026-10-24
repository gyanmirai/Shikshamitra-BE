using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	[Authorize(AssignPermission)]
	public async Task<AssignmentBulkPreviewDto> PreviewBulkAsync(BulkAssignStudentFeeStructureDto input)
	{
		var result = new AssignmentBulkPreviewDto();
		foreach (var item in BulkItems(input))
		{
			try { result.Results.Add(new() { StudentId = item.StudentId, Preview = await PreviewAsync(item) }); }
			catch (BusinessException ex) { result.Results.Add(new() { StudentId = item.StudentId, ErrorCode = ex.Code, ErrorMessage = AssignmentErrorMessage(ex.Code) }); }
			catch (Exception ex) { Logger.LogWarning(ex, "A bulk assignment preview failed"); result.Results.Add(new() { StudentId = item.StudentId, ErrorCode = AssignmentErrorCodes.InvalidInput, ErrorMessage = "Preview unavailable. Check access and configuration." }); }
		}
		return result;
	}

	[Authorize(AssignPermission)]
	public async Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input)
	{
		var acceptedAt = Clock.Now;
		var result = new BulkAssignStudentFeeStructureResultDto();
		foreach (var item in BulkItems(input))
		{
			try
			{
				var committed = await AssignAcceptedAsync(item, acceptedAt);
				result.Results.Add(new() { StudentId = item.StudentId, Outcome = committed.Outcome, Success = true, Result = committed });
				if (committed.Outcome == "AlreadyAssigned") result.AlreadyAssignedCount++; else result.SuccessCount++;
			}
			catch (BusinessException ex)
			{
				var skip = ex.Code == AssignmentErrorCodes.AlreadyAssigned || ex.Code == AssignmentErrorCodes.InactiveEnrollment;
				result.Results.Add(new() { StudentId = item.StudentId, Outcome = skip ? "Skipped" : "Failed", ErrorCode = ex.Code, ErrorMessage = AssignmentErrorMessage(ex.Code) });
				if (skip) result.SkippedCount++; else result.FailedCount++;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "A bulk assignment item failed");
				result.Results.Add(new() { StudentId = item.StudentId, Outcome = "Failed", ErrorCode = AssignmentErrorCodes.CommitFailed, ErrorMessage = "Assignment could not be completed. Check access and retry this student." });
				result.FailedCount++;
			}
		}
		return result;
	}

	private static IReadOnlyList<AssignStudentFeeStructureDto> BulkItems(BulkAssignStudentFeeStructureDto input)
	{
		var items = input.Items;
		if (items == null || input.StudentIds == null) throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		if (items.Count == 0) items = input.StudentIds.Select(id => new AssignStudentFeeStructureDto {
			StudentId = id, AcademicYearId = input.AcademicYearId, StandardFeeStructureId = input.StandardFeeStructureId,
			SelectedOptionalFeeHeadIds = input.SelectedOptionalFeeHeadIds, FeeHeadDetails = input.FeeHeadDetails,
			ChargeStartDate = input.ChargeStartDate, ChargeEndDate = input.ChargeEndDate, ChargePeriodSource = input.ChargePeriodSource,
			ChargePeriodSourceReferenceId = input.ChargePeriodSourceReferenceId, ChargePeriodOverrideReason = input.ChargePeriodOverrideReason }).ToList();
		if (items.Count is < 1 or > 100 || items.Any(x => x == null) || items.Select(x => x.StudentId).Distinct().Count() != items.Count)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		return items;
	}

	private static string AssignmentErrorMessage(string? code) => code switch {
		AssignmentErrorCodes.VariableAmountRequired => "Enter an amount for every included variable fee head before previewing.",
		AssignmentErrorCodes.AlreadyAssigned => "A different fee assignment already exists for this year. View its details before making a correction.",
		AssignmentErrorCodes.StalePreview => "The preview expired or its inputs changed. Preview this student again.",
		AssignmentErrorCodes.EnrollmentRequired => "No unambiguous accessible enrollment exists for this student and year.",
		AssignmentErrorCodes.InactiveEnrollment => "The enrollment is inactive. Resolve the student's enrollment before assignment.",
		AssignmentErrorCodes.SourceUnavailable => "Service dates could not be verified. Select a supported source or an authorized manual override.",
		AssignmentErrorCodes.StructureMismatch => "Select a published structure matching this enrollment and academic year.",
		AssignmentErrorCodes.ConfigurationInvalid => "Check the published structure, payment terms and attached policy versions.",
		_ => "Check this student's configuration and inputs, then preview again."
	};
}
