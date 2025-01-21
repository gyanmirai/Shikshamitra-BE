using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public interface IStandardAppService :
    ICrudAppService<
        StandardDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateStandardDto>
{
	Task<List<StandardDto>> GetStandardsByEducationMediumId(Guid educationMediumId);
}