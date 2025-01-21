using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public interface IEducationMediumStageAppService : ICrudAppService<
	EducationMediumStageDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumStageDto>;