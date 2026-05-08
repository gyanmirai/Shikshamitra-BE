using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public class StudentFeeStructureAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository
) : ApplicationService, IStudentFeeStructureAppService
{
	public async Task AssignAsync(AssignStudentFeeStructureDto input)
	{
		// get the student
		var student = await studentRepository.GetAsync(input.StudentId) ?? throw new BusinessException("Student with given id does not exist.");

		if (student.FeeStructureId != null)
		{
			throw new BusinessException("Fee structure is already assigned to the student. Please unassign the existing fee structure before assigning a new one.");
		}

		// validate the standard fee structure id
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId) ?? throw new BusinessException("Fee structure with given id does not exist.");

		// assign the fee structure to the student
		student.AssignFeeStructure(input.StandardFeeStructureId);

		// TODO: Generate student due fees based on the assigned fee structure and optional fee heads
	}

	//public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	//{
	//}
}
