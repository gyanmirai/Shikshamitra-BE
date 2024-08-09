using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Onboarding.EducationBoards;

public interface IEducationMediumStageAppService : ICrudAppService<
	EducationMediumStageDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumStageDto>;