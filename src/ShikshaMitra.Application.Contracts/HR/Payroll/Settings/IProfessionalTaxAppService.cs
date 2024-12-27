using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProfessionalTaxAppService : ICrudAppService<ProfessionalTaxDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProfessionalTaxDto>
{
	
}