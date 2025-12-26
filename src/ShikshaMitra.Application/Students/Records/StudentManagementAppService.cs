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
}
