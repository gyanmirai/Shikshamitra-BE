using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public interface IEducationMediumStageAppService : ICrudAppService<
	EducationMediumStageDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumStageDto>;