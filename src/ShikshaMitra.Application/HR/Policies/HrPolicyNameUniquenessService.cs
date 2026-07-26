using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyNameUniquenessService(
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IRepository<AttendancePolicy, Guid> attendanceRepository,
	IRepository<PayrollPolicy, Guid> payrollRepository) : ITransientDependency
{
	private const string WorkCalendarConstraint = "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name";
	private const string AttendanceConstraint = "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name";
	private const string PayrollConstraint = "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name";
	private const string LeaveConstraint = "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName";

	public async Task EnsureWorkCalendarAvailableAsync(
		Guid academicYearId,
		string name,
		Guid? excludeId = null)
	{
		var normalizedName = name.Trim();
		await EnsureAvailableAsync(
			"work calendar",
			normalizedName,
			workCalendarRepository.AnyAsync(policy =>
				policy.AcademicYearId == academicYearId &&
				policy.Status != HrPolicyStatus.Archived &&
				policy.Name == normalizedName &&
				(!excludeId.HasValue || policy.Id != excludeId.Value)));
	}

	public async Task EnsureAttendanceAvailableAsync(
		Guid academicYearId,
		string name,
		Guid? excludeId = null)
	{
		var normalizedName = name.Trim();
		await EnsureAvailableAsync(
			"attendance",
			normalizedName,
			attendanceRepository.AnyAsync(policy =>
				policy.AcademicYearId == academicYearId &&
				policy.Status != HrPolicyStatus.Archived &&
				policy.Name == normalizedName &&
				(!excludeId.HasValue || policy.Id != excludeId.Value)));
	}

	public async Task EnsurePayrollAvailableAsync(
		Guid academicYearId,
		string name,
		Guid? excludeId = null)
	{
		var normalizedName = name.Trim();
		await EnsureAvailableAsync(
			"payroll",
			normalizedName,
			payrollRepository.AnyAsync(policy =>
				policy.AcademicYearId == academicYearId &&
				policy.Status != HrPolicyStatus.Archived &&
				policy.Name == normalizedName &&
				(!excludeId.HasValue || policy.Id != excludeId.Value)));
	}

	public Task ExecuteWorkCalendarWriteAsync(string name, Func<Task> writeAsync) =>
		ExecuteWriteAsync("work calendar", name, WorkCalendarConstraint, HRErrorCodes.PolicyNameAlreadyExists, writeAsync);

	public Task ExecuteAttendanceWriteAsync(string name, Func<Task> writeAsync) =>
		ExecuteWriteAsync("attendance", name, AttendanceConstraint, HRErrorCodes.PolicyNameAlreadyExists, writeAsync);

	public Task ExecutePayrollWriteAsync(string name, Func<Task> writeAsync) =>
		ExecuteWriteAsync("payroll", name, PayrollConstraint, HRErrorCodes.PolicyNameAlreadyExists, writeAsync);

	public Task ExecuteLeaveWriteAsync(string name, Func<Task> writeAsync) =>
		ExecuteWriteAsync("leave", name, LeaveConstraint, HRErrorCodes.LeavePolicyNameAlreadyExists, writeAsync);

	private static async Task EnsureAvailableAsync(
		string policyType,
		string policyName,
		Task<bool> existsTask)
	{
		if (!await existsTask)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PolicyNameAlreadyExists)
			.WithData("policyType", policyType)
			.WithData("policyName", policyName.Trim());
	}

	private static async Task ExecuteWriteAsync(
		string policyType,
		string policyName,
		string constraintName,
		string errorCode,
		Func<Task> writeAsync)
	{
		try
		{
			await writeAsync();
		}
		catch (Exception ex) when (IsUniqueNameConstraintViolation(ex, constraintName))
		{
			throw new BusinessException(errorCode, innerException: ex)
				.WithData("policyType", policyType)
				.WithData("policyName", policyName.Trim());
		}
	}

	private static bool IsUniqueNameConstraintViolation(Exception exception, string constraintName)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
