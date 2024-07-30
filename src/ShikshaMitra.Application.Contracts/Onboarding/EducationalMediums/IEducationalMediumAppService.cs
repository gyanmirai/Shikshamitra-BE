using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Onboarding.EducationalMediums;

public interface IEducationalMediumAppService :
    ICrudAppService<
        EducationalMediumDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateEducationalMediumDto>
{
}