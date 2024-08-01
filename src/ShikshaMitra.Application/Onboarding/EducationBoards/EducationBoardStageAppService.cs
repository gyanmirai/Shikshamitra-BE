using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardStageAppService : CrudAppService<
       EducationBoardStage,
          EducationBoardStageDto,
             Guid,
                PagedAndSortedResultRequestDto,
                   CreateUpdateEducationBoardStageDto>,
    IEducationBoardStageAppService
{
    public EducationBoardStageAppService(IRepository<EducationBoardStage, Guid> repository) : base(repository)
    {
    }
}
