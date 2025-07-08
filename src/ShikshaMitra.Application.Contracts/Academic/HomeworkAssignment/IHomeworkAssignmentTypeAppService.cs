using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public interface IHomeworkAssignmentTypeAppService
	: ICrudAppService<
		HomeworkAssignmentTypeDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateHomeworkAssignmentTypeDto
	>
{ }