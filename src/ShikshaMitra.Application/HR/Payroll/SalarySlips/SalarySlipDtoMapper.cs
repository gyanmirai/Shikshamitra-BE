using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipDtoMapper(
	IEmployeePayrollDetailsRepository employeePayrollDetailsRepository,
	SalarySlipSnapshotReader snapshotReader,
	PayslipIssuanceService issuance) : ITransientDependency
{
	public async Task<SalarySlipDto> ToDtoAsync(SalarySlip salarySlip, Employee employee)
	{
		if (salarySlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			return PayslipIssuanceService.ReadFacts(await issuance.GetCurrentAsync(salarySlip.Id), salarySlip.Status);
		var snapshot = await snapshotReader.GetAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
		var payrollDetails = await employeePayrollDetailsRepository.GetByEmployeeIdAsync(salarySlip.EmployeeId);
		return SalarySlipProjection.ToDto(salarySlip, employee, snapshot, payrollDetails);
	}
}
