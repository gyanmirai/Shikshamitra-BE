using ShikshaMitra.Students.Records.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentManagementAppService(
	IRepository<Student, Guid> studentRepository
) : ApplicationService, IStudentManagementAppService
{
	public async Task<List<StudentRecordDto>> GetStudentRecords(Guid divisionId)
	{
		try
		{
			var students = await studentRepository.GetListAsync(
				s => s.DivisionId == divisionId,
				includeDetails: true
			);

			var result = new List<StudentRecordDto>();
			foreach (var student in students)
			{
				result.Add(new StudentRecordDto
				{
					StudentId = student.Id,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}",
					GrNo = student.GrNo,
					RollNo = student.RollNo,
					HouseSettingId = student.AdditionalDetail?.HouseId,
					HouseSetting = student.AdditionalDetail?.House != null
						? new HouseSettingsDto
						{
							Name = student.AdditionalDetail.House.Name,
							Color = student.AdditionalDetail.House.Color,
							LogoUrl = student.AdditionalDetail.House.LogoUrl,
							Description = student.AdditionalDetail.House.Description,
						}
						: null,
					IsDivisionHouseLeader = student.AdditionalDetail?.IsDivisionHouseLeader,
					IsGlobalHouseLeader = student.AdditionalDetail?.IsGlobalHouseLeader
				});
			}

			return result;
		}
		catch 
		{ 
			throw new UserFriendlyException("An error occurred while fetching student records.");
		}
	}
	public async Task AssignHouseToStudent(Guid studentId, Guid houseId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// assign house to student only if he is not assigned with other house
		if (student.AdditionalDetail?.HouseId != null)
		{
			throw new UserFriendlyException("Student is already assigned to a house.");
		}
		student.AssignHouse(houseId);
		await studentRepository.UpdateAsync(student);
	}

	public async Task AssignHouseGlobalLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// Global leader can be assigned to only one student in whole medium
		var globalLeader = await studentRepository.FirstOrDefaultAsync(s => s.AcademicMediumId == student.AcademicMediumId && s.AdditionalDetail != null && s.AdditionalDetail!.IsGlobalHouseLeader == true);
		if (globalLeader != null)
		{
			throw new UserFriendlyException("Global leader can be assigned to only one student in whole medium.");
		}
		student.AssignGlobalLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task AssignHouseDivisionLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// Division leader can be assigned to only one student in student's division
		var divisionLeader = await studentRepository.FirstOrDefaultAsync(s => s.DivisionId == student.DivisionId && s.AdditionalDetail != null && s.AdditionalDetail!.IsDivisionHouseLeader == true);
		if (divisionLeader != null)
		{
			throw new UserFriendlyException("Division leader can be assigned to only one student in student's division.");
		}
		student.AssignDivisionLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseFromStudent(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// If student is assigned with a house
		if (student.AdditionalDetail?.HouseId == null)
		{
			throw new UserFriendlyException("Student is not assigned with a house.");
		}
		student.DeassignHouse();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseGlobalLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// If student is not assigned with a house or he is not global leader
		if (student.AdditionalDetail?.HouseId == null || student.AdditionalDetail!.IsGlobalHouseLeader == false)
		{
			throw new UserFriendlyException("Student is not assigned with a house or student is not global leader.");
		}
		student.DeassignGlobalLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseDivisionLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		// If student is not assigned with a house or he is not division leader
		if (student.AdditionalDetail?.HouseId == null || student.AdditionalDetail!.IsDivisionHouseLeader == false)
		{
			throw new UserFriendlyException("Student is not assigned with a house or student is not division leader.");
		}
		student.DeassignDivisionLeader();
		await studentRepository.UpdateAsync(student);
	}
}
