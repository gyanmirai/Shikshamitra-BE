using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.Standards;

public interface IStandardAppService :
    ICrudAppService<
        StandardDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateStandardDto>
{
}