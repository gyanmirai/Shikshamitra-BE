using ShikshaMitra.Academic.Homework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class HomeworkAssignmentTypeAppService(
	IRepository<HomeworkAssignmentType, Guid> workTypeRepository
)
	: CrudAppService<
		HomeworkAssignmentType,
		HomeworkAssignmentTypeDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateHomeworkAssignmentTypeDto
	>(workTypeRepository),
		IHomeworkAssignmentTypeAppService
{
	// implement API to fetch HomeworkAssignmentType by AcademicMediumId
	public async Task<List<HomeworkAssignmentTypeDto>> GetByAcademicMediumIdAsync(
		Guid academicMediumId
	)
	{
		try
		{
			var workTypes = await workTypeRepository.GetListAsync(x =>
				x.AcademicMediumId == academicMediumId
			);
			return ObjectMapper.Map<List<HomeworkAssignmentType>, List<HomeworkAssignmentTypeDto>>(
				workTypes
			);
		}
		catch (Exception ex)
		{
			throw new ApplicationException(
				"An error occurred while fetching HomeworkAssignmentTypes by EducationMediumId.",
				ex
			);
		}
	}
}

