using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees.Payroll;

public class EmployeePayrollContractWorkflow(
	IEmployeeRepository employeeRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IHrPolicyAssignmentAppService policyAssignmentAppService,
	EmployeePayrollComponentResolver componentResolver,
	EmployeePayrollContractWriter contractWriter) : ITransientDependency
{
	public async Task UpdateAsync(Guid employeeId, PayrollContract input)
	{
		EmployeePayrollRules.ValidatePayrollContract(input);

		var employee = await employeeRepository.GetAsync(employeeId);
		var existingEarnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var existingDeductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var earningLines = await componentResolver.ResolveEarningsAsync(input.EarningComponents, existingEarnings);
		var deductionLines = await componentResolver.ResolveDeductionsAsync(input.DeductionComponents, existingDeductions);

		await contractWriter.SaveAsync(employeeId, input, earningLines, deductionLines);

		if (string.Equals(input.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase))
		{
			await DisableDirectPolicyOverrideAsync(employee, input.PolicyOverride);
		}
		else if (input.PolicyOverride != null)
		{
			await UpsertDirectPolicyOverrideAsync(employee, input.PolicyOverride);
		}
	}

	private async Task UpsertDirectPolicyOverrideAsync(Employee employee, EmployeePayrollPolicyOverrideContract input)
	{
		await policyAssignmentAppService.UpsertEmployeeOverrideAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = input.AcademicYearId,
			AssignmentId = input.AssignmentId,
			AttendancePolicyId = input.AttendancePolicyId,
			PayrollPolicyId = input.PayrollPolicyId,
			LeavePolicyId = input.LeavePolicyId,
			EffectiveFrom = input.EffectiveFrom,
			EffectiveTo = input.EffectiveTo,
			AssignmentReason = input.AssignmentReason,
			ConfirmationToken = input.ConfirmationToken
		});
	}

	private async Task DisableDirectPolicyOverrideAsync(Employee employee, EmployeePayrollPolicyOverrideContract? input)
	{
		if (input?.AcademicYearId is not { } academicYearId || academicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
		}

		await policyAssignmentAppService.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = academicYearId,
			AssignmentId = input.AssignmentId,
			EffectiveDate = input.EffectiveFrom.Date,
			AssignmentReason = input.AssignmentReason,
			ConfirmationToken = input.ConfirmationToken
		});
	}
}
