using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardAppService : CrudAppService<
    EducationBoard,
    EducationBoardDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateEducationBoardDto>, IEducationBoardAppService
{
    public EducationBoardAppService(IRepository<EducationBoard, Guid> repository) : base(repository)
    {
    }
}