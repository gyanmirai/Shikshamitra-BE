using ShikshaMitra.HR.Payroll.PayrollStructures;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.PayrollStructures;

public class PayrollStructureAppService(IPayrollStructureRepository repository)
	: CrudAppService<Payroll.PayrollStructures.PayrollStructure, PayrollStructureDto, Guid,
		PagedAndSortedResultRequestDto, CreateUpdatePayrollStructureDto>(repository), IPayrollStructureAppService
{
}