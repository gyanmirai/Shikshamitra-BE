using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.EducationMediums;

public interface IEducationMediumAppService :
    ICrudAppService<
        EducationMediumDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateEducationMediumDto>
{
}