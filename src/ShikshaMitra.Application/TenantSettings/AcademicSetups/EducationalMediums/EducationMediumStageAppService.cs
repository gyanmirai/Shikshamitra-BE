using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationBoards;

[Authorize]
public class EducationMediumStageAppService(IRepository<EducationMediumStage, Guid> repository) : CrudAppService<
		EducationMediumStage,
		EducationMediumStageDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateEducationMediumStageDto>(repository),
	IEducationMediumStageAppService;