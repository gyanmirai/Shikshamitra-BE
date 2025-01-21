using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public interface IEducationMediumAppService :
    ICrudAppService<
        EducationMediumDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateEducationMediumDto>
{
}