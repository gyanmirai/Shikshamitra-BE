using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentRegistrationAppService(EntityValidationService entityValidationService,
	StudentQueryService studentQueryService) : ApplicationService, IStudentRegistrationAppService
{
	public async Task<PagedResultDto<StudentRegistrationFormDto>> GetRegistrationFormsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null)
	{
		try
		{
			await entityValidationService.ValidateAcademicMediumExistsAsync(academicMediumId);

			var query = await studentQueryService.GetStudentWithDetailsQueryAsync(studentFilter: s =>
				s.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id
			);

			var totalCount = query.Count();
			var pagedQuery = query.Skip(skipCount).Take(maxResultCount);

			var results = await AsyncExecuter.ToListAsync(pagedQuery);
			var resultList = ObjectMapper.Map<List<StudentQueryResult>, List<StudentRegistrationFormDto>>(
				results
			);

			return new PagedResultDto<StudentRegistrationFormDto>(totalCount, resultList.ToList());
		}
		catch (Exception ex)
		{
			// Handle exception as needed
			throw;
		}
	}

	public async Task<PagedResultDto<StudentRegistrationDto>> GetRegistrationsByMediumAsync(Guid academicMediumId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null)
	{
		try
		{
			await entityValidationService.ValidateAcademicMediumExistsAsync(academicMediumId);

			var query = await studentQueryService.GetStudentWithDetailsQueryAsync(studentFilter: s =>
				s.Status == StudentLifeCycleStatusEnum.Registation
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id
			);

			var totalCount = query.Count();
			var pagedQuery = query.Skip(skipCount).Take(maxResultCount);

			var results = await AsyncExecuter.ToListAsync(pagedQuery);
			var resultList = ObjectMapper.Map<List<StudentQueryResult>, List<StudentRegistrationDto>>(
				results
			);

			return new PagedResultDto<StudentRegistrationDto>(totalCount, resultList.ToList());
		}
		catch (Exception ex)
		{
			// Handle exception as needed
			throw;
		}
	}
}
