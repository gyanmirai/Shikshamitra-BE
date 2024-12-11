using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public interface IPayrollStructureAppService : ICrudAppService<PayrollStructureDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePayrollStructureDto>
{
	
}