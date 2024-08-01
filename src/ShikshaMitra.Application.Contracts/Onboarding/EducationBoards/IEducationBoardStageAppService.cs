using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Onboarding.EducationBoards;

public interface IEducationBoardStageAppService : ICrudAppService<
           EducationBoardStageDto,
                  Guid,
                         PagedAndSortedResultRequestDto,
                                CreateUpdateEducationBoardStageDto>
{
}
