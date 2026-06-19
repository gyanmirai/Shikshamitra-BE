using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	public async Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input)
	{
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new Volo.Abp.BusinessException("Fee structure with given id does not exist.");

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var metadata     = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
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

				if (student.FeeStructureId != null)
				{
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId, Success = false, ErrorMessage = "Fee structure already assigned."
					});
					result.SkippedCount++;
					continue;
				}

				student.AssignFeeStructure(input.StandardFeeStructureId);
				await studentFeeStructureRepository.InsertAsync(
					new StudentFeeStructure(
						id: GuidGenerator.Create(),
						tenantId: CurrentTenant.Id,
						studentId: student.Id,
						feeStructureStandardId: feeStructure.Id),
					autoSave: false);

				var dueFees = await dueFeeManager.GenerateForStudentAsync(
					student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds, optionalDetails);
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
