using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentRegistrationAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository
) : ApplicationService, IStudentRegistrationAppService
{
	public async Task<PagedResultDto<StudentRegistrationFormDto>> GetRegistrationFormsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null)
	{
		await CheckAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with aggregate parts auto-included
		var queryable = await studentRepository.GetQueryableAsync();
		
		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);

		// Implement search functionality
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.RegistrationFormNo != null && x.AdditionalDetail.RegistrationFormNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null && x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.Email != null && x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm))
			);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		
		if (totalCount == 0)
		{
			return new PagedResultDto<StudentRegistrationFormDto>(0, new List<StudentRegistrationFormDto>());
		}

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentRegistrationFormDto>>(results);

		return new PagedResultDto<StudentRegistrationFormDto>(totalCount, resultList);
	}

	public async Task<PagedResultDto<StudentRegistrationDto>> GetRegistrationsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null)
	{
		await CheckAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with aggregate parts auto-included
		var queryable = await studentRepository.GetQueryableAsync();
		
		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Registation
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);

		// Implement search functionality
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.RegistrationFormNo != null && x.AdditionalDetail.RegistrationFormNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null && x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.Email != null && x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm))
			);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		
		if (totalCount == 0)
		{
			return new PagedResultDto<StudentRegistrationDto>(0, new List<StudentRegistrationDto>());
		}

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentRegistrationDto>>(results);

		return new PagedResultDto<StudentRegistrationDto>(totalCount, resultList);
	}

	private async Task CheckAcademicMediumExistsAsync(Guid academicMediumId)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == academicMediumId))
		{
			throw new BusinessException("", L["Academic Medium Not Found"]);
		}
	}
}
