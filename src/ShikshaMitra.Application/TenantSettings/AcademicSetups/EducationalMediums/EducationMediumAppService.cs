using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Onboarding.EducationBoards;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationMediums;

[Authorize]
public class EducationMediumAppService : CrudAppService<
    EducationMedium,
    EducationMediumDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateEducationMediumDto>, IEducationMediumAppService
{
    public EducationMediumAppService(IRepository<EducationMedium, Guid> repository) : base(repository)
    {
    }
}
