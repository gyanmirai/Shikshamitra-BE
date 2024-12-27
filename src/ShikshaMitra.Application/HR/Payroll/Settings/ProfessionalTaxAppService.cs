using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTaxAppService(IProfessionalTaxRepository professionalTaxRepository) : CrudAppService<HR.Payroll.Settings.ProfessionalTax, ProfessionalTaxDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProfessionalTaxDto>(professionalTaxRepository), IProfessionalTaxAppService
{
	
}