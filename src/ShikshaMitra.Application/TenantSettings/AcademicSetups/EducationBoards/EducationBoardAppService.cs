using Microsoft.AspNetCore.Authorization;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationBoards;

[Authorize]
public class EducationBoardAppService(IRepository<EducationBoard, Guid> repository) : CrudAppService<
    EducationBoard,
    EducationBoardDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateEducationBoardDto>(repository), IEducationBoardAppService
{
}