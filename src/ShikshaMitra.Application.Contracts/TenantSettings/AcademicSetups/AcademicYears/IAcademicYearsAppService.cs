using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;

public interface IAcademicYearsAppService : ICrudAppService<AcademicYearDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateAcademicYearDto>
{
}
