using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public interface IDivisionAppService :
    ICrudAppService<
        DivisionDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateDivisionDto>
{
}