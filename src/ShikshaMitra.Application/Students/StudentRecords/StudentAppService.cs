using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentAppService(StudentManager studentManager ,IStudentRepository studentRepository, IEducationBoardRepository educationBoardRepository) :
	CrudAppService<Student, StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>(studentRepository),
	IStudentAppService
{
	// Get GR No

	public async Task<int> GetGrNoAsync(Guid educationBoardId, Guid educationMediumId, Guid educationMediumStageId)
	{
		EducationBoard? educationBoard = await educationBoardRepository.GetAsync(educationBoardId);

		if (educationBoard.Type == EducationBoardType.GSEB)
		{
			Student? student = await studentRepository.GetLastStudentbyGrNoForGSEBAsync(educationBoardId, educationMediumId, educationMediumStageId);

			return student != null ? student.GrNoRecord + 1 : 1;
		}
		else if (educationBoard.Type == EducationBoardType.CBSE)
		{
			Student? student = await studentRepository.GetLastStudentbyGrNoForCBSEAsync(educationBoardId);

			return student != null ? student.GrNoRecord + 1 : 1;
		}
		else
		{
			// if education board is not found then throw an exception
			throw new UserFriendlyException("Education Board not found");
		}
	}

	// Get Student Roll No
	public async Task<int> GetRollNoAsync(Guid divisionId)
	{
		Student? student = await studentRepository.GetLastStudentbyRollNoAsync(divisionId);
		return student != null ? student.RollNo + 1 : 1;
	}

	public async Task UpdateGrNumbersAsync(CreateUpdateGRNoDto input)
	{
		if (input.StudentNumbers == null || input.StudentNumbers.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}

		foreach (var item in input.StudentNumbers)
		{
			// Validate using the domain service.
			await studentManager.ValidateGrNumberAsync(item.StudentId, item.GrNo);

			// After validation, fetch and update the student.
			var student = await studentRepository.GetAsync(item.StudentId);
			student.GrNoRecord = item.GrNo;
			await studentRepository.UpdateAsync(student);
		}
	}

	public async Task UpdateRollNumbersAsync(CreateUpdateRollNoDto input)
	{
		if (input.StudentNumbers == null || input.StudentNumbers.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}
		foreach (var item in input.StudentNumbers)
		{
			// Validate using the domain service.
			await studentManager.ValidateRollNumberAsync(item.StudentId, item.RollNo);
			// After validation, fetch and update the student.
			var student = await studentRepository.GetAsync(item.StudentId);
			student.RollNo = item.RollNo;
			await studentRepository.UpdateAsync(student);
		}
	}
}
