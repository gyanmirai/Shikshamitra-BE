using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructureAppService(
	IPayrollStructureRepository payrollStructureRepository,
	IEmployeeRepository employeeRepository,
	PayrollStructureManager payrollStructureManager)
	: CrudAppService<PayrollStructure, PayrollStructureDto, Guid,
			PagedAndSortedResultRequestDto, CreateUpdatePayrollStructureDto>(payrollStructureRepository),
		IPayrollStructureAppService
{
	//public async Task<List<PayrollStructureDto>> GetPayrollStructuresByEducationMediumStageId(
	//	Guid educationMediumStageId)
	//{
	//	IQueryable<PayrollStructure> query = await payrollStructureRepository.GetQueryableAsync();
	//	query = query.Where(x => x.EducationMediumStageId == educationMediumStageId);
	//	return ObjectMapper.Map<List<PayrollStructure>, List<PayrollStructureDto>>(
	//		await AsyncExecuter.ToListAsync(query));
	//}



	//public async Task<List<PayrollStructureDto>> GetPayrollStructuresByEmployeeId(Guid employeeId)
	//{
	//	var employee = await employeeRepository.GetAsync(employeeId) ?? throw new Volo.Abp.UserFriendlyException("Employee not found");

	//	// Check if the employee has a specific payroll structure
	//	if (employee.PayrollStructureId.HasValue)
	//	{
	//		var payrollStructure = await employeeRepository.GetAsync(employee.PayrollStructureId.Value);
	//		return new List<PayrollStructureDto> { ObjectMapper.Map<PayrollStructure, PayrollStructureDto>(payrollStructure) };
	//	}

	//	// Otherwise, get the default payroll structure for the employee's EducationMediumStage
	//	IQueryable<PayrollStructure> query = await payrollStructureRepository.GetQueryableAsync();
	//	query = query.Where(x => x.EducationMediumStageId == employee.EducationMediumStageId);
	//	return ObjectMapper.Map<List<PayrollStructure>, List<PayrollStructureDto>>(
	//		await AsyncExecuter.ToListAsync(query));
	//}
	public async Task<List<PayrollStructureDto>> GetPayrollStructuresByEmployeeId(Guid employeeId)
	{
		IQueryable<PayrollStructure> query = await Repository.GetQueryableAsync();
		query = query.Where(x => x.EmployeeId == employeeId);
		return ObjectMapper.Map<List<PayrollStructure>, List<PayrollStructureDto>>(
			await AsyncExecuter.ToListAsync(query));
	}
}