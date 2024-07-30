using ShikshaMitra.Onboarding.EducationBoards;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.EducationalMediums;

public class EducationalMediumAppService : CrudAppService<
    EducationalMedium,
    EducationalMediumDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateEducationalMediumDto>, IEducationalMediumAppService
{
    public EducationalMediumAppService(IRepository<EducationalMedium, Guid> repository) : base(repository)
    {
    }
}
