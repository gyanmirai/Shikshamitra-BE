using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	[Microsoft.AspNetCore.Authorization.Authorize(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.Add)]
	public async Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input)
	{
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new Volo.Abp.BusinessException("Fee structure with given id does not exist.");
		if (!feeStructure.IsLocked)
			throw new Volo.Abp.BusinessException("Finance:FeeStructureMustBeLocked");

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var metadata     = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
		var concessionSnapshot = await ValidateConcessionPolicySnapshotAsync(feeStructure, academicYear.Id);
		var collectionSnapshot = await ValidateCollectionPolicySnapshotAsync(feeStructure, academicYear.Id);
		var refundSnapshot = await ValidateRefundPolicySnapshotAsync(feeStructure, academicYear.Id);
		var prorationSnapshot = await ValidateProrationPolicySnapshotAsync(feeStructure, academicYear.Id);
		var chargePeriod = await ResolveChargePeriodAsync(
			input.ChargeStartDate, input.ChargeEndDate, input.ChargePeriodSource,
			input.ChargePeriodSourceReferenceId, input.ChargePeriodOverrideReason, academicYear);
		var requestFingerprint = BuildAssignmentFingerprint(
			input.StandardFeeStructureId, input.SelectedOptionalFeeHeadIds, input.FeeHeadDetails,
			chargePeriod.Start, chargePeriod.End, chargePeriod.Source,
			chargePeriod.SourceReferenceId, chargePeriod.OverrideReason);
		var optionalDetails = BuildOptionalDetails(input.FeeHeadDetails);
		var result = new BulkAssignStudentFeeStructureResultDto();

		foreach (var studentId in input.StudentIds)
		{
			try
			{
				var student = await studentRepository.GetAsync(studentId);
				if (student == null)
				{
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId, Success = false, ErrorMessage = "Student not found."
					});
					result.SkippedCount++;
					continue;
				}

				await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);
				if (student.StandardId != feeStructure.StandardId)
				{
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId, Success = false,
						ErrorMessage = "Student standard does not match the fee structure standard."
					});
					result.SkippedCount++;
					continue;
				}

				var existingAssignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
					x.StudentId == student.Id && x.AcademicYearId == academicYear.Id);
				if (existingAssignment != null)
				{
					if (string.Equals(
						existingAssignment.AssignmentRequestFingerprint,
						requestFingerprint,
						StringComparison.Ordinal))
					{
						result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
						{
							StudentId = studentId, Success = true,
							ErrorMessage = "Already assigned; treated as an idempotent retry."
						});
						result.SuccessCount++;
						continue;
					}
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId, Success = false, ErrorMessage = "Fee structure already assigned."
					});
					result.SkippedCount++;
					continue;
				}

				student.AssignFeeStructure(input.StandardFeeStructureId);
				var assignmentId = GuidGenerator.Create();
				await studentFeeStructureRepository.InsertAsync(
					new StudentFeeStructure(
						id: assignmentId,
						tenantId: CurrentTenant.Id,
						studentId: student.Id,
						feeStructureStandardId: feeStructure.Id,
						academicYearId: academicYear.Id,
						concessionPolicyId: concessionSnapshot.Policy.Id,
						concessionPolicyVersionId: concessionSnapshot.Version.Id,
						concessionFeeHeadScope: feeStructure.ConcessionFeeHeadScope,
						concessionApplicableFeeHeadIds: feeStructure.GetConcessionApplicableFeeHeadIds(),
						collectionPolicyId: collectionSnapshot.Policy.Id,
						collectionPolicyVersionId: collectionSnapshot.Version.Id,
						refundPolicyId: refundSnapshot?.Policy.Id,
						refundPolicyVersionId: refundSnapshot?.Version.Id,
						prorationPolicyId: prorationSnapshot?.Policy.Id,
						prorationPolicyVersionId: prorationSnapshot?.Version.Id,
						chargeStartDate: chargePeriod.Start,
						chargeEndDate: chargePeriod.End,
						chargePeriodSource: chargePeriod.Source,
						chargePeriodSourceReferenceId: chargePeriod.SourceReferenceId,
						chargePeriodOverrideReason: chargePeriod.OverrideReason,
						assignmentRequestFingerprint: requestFingerprint),
					autoSave: false);

				var dueFees = await dueFeeManager.GenerateForStudentAsync(
					student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds, optionalDetails,
					prorationSnapshot?.Snapshot, prorationSnapshot?.Version.Id, chargePeriod.Start, chargePeriod.End);
				await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);

				result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto { StudentId = studentId, Success = true });
				result.SuccessCount++;
			}
			catch (Exception ex)
			{
				result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
				{
					StudentId = studentId, Success = false, ErrorMessage = ex.Message
				});
				result.SkippedCount++;
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return result;
	}
}
