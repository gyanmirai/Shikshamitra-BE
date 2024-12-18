using ShikshaMitra.HR.Payroll.PayrollStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.PayrollStructures;

public class PayrollStructureAppService(IPayrollStructureRepository payrollStructureRepository)
	: CrudAppService<PayrollStructure, PayrollStructureDto, Guid,
			PagedAndSortedResultRequestDto, CreateUpdatePayrollStructureDto>(payrollStructureRepository),
		IPayrollStructureAppService
{
	public async Task<List<PayrollStructureDto>> GetPayrollStructuresByEducationMediumStageId(
		Guid educationMediumStageId)
	{
		IQueryable<PayrollStructure> query = (await payrollStructureRepository.GetQueryableAsync());
		query = query.Where(x => x.EducationMediumStageId == educationMediumStageId);
		return ObjectMapper.Map<List<PayrollStructure>, List<PayrollStructureDto>>(
			await AsyncExecuter.ToListAsync(query));
	}
}