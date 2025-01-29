using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public interface IPayrollStructureAppService : ICrudAppService<PayrollStructureDto, Guid, PagedAndSortedResultRequestDto
	, CreateUpdatePayrollStructureDto>
{
	//Task<List<PayrollStructureDto>> GetPayrollStructuresByEducationMediumStageId(
	//	Guid educationMediumStageId);

	Task<List<PayrollStructureDto>> GetPayrollStructuresByEmployeeId(Guid employeeId);
}