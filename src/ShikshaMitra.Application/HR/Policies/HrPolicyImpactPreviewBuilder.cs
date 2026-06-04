using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyImpactPreviewBuilder(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyWorkCalendarReferenceResolver workCalendarReferenceResolver,
	HrPolicyAffectedEmployeeResolver affectedEmployeeResolver,
	HrPolicyPayrollRiskReader payrollRiskReader,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HrPolicyImpactPreviewDto> BuildAsync(HrPolicyImpactPreviewRequestDto input)
	{
		var policyType = NormalizePolicyType(input.PolicyType);
		var academicYearId = input.AcademicYearId ?? await ResolveAcademicYearIdAsync(policyType, input.PolicyId, input);
		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = policyType,
			PolicyId = input.PolicyId,
			AcademicYearId = academicYearId,
			Action = string.IsNullOrWhiteSpace(input.Action) ? "update" : input.Action.Trim().ToLowerInvariant()
		};

		await FillCurrentVersionAsync(preview);
		preview.ProposedVersion = preview.CurrentVersion.HasValue ? preview.CurrentVersion.Value + 1 : 1;
		var includeDraftReferences = string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase);
		var workCalendarRefs = await workCalendarReferenceResolver.ResolveAsync(policyType, input.PolicyId, academicYearId, includeDraftReferences);

		var affectedAssignments = await GetAffectedAssignmentsAsync(policyType, input.PolicyId, academicYearId, workCalendarRefs);
		preview.AffectedAssignmentCount = affectedAssignments.Count;

		var affectedEmployees = await affectedEmployeeResolver.ResolveAsync(academicYearId, affectedAssignments);
		preview.AffectedEmployeeCount = affectedEmployees.Count;
		preview.AffectedEmployees = affectedEmployees.Sample.Select(ToAffectedEmployeeDto).ToList();

		await payrollRiskReader.FillPayrollRiskAsync(preview, policyType, input.PolicyId);
		HrPolicyImpactWarningBuilder.AddWorkCalendarReferenceWarning(preview, workCalendarRefs);
		HrPolicyImpactWarningBuilder.AddWarnings(preview);
		await confirmationService.AttachTokenAsync(preview);
		return preview;
	}

	public static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" => "work_calendar",
		"payroll" => "payroll",
		"attendance" => "attendance",
		_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
	};

	private async Task<Guid> ResolveAcademicYearIdAsync(string policyType, Guid? policyId, HrPolicyImpactPreviewRequestDto input)
	{
		if (policyType == "work_calendar" && input.WorkCalendarPolicy?.AcademicYearId is Guid calendarYear && calendarYear != Guid.Empty)
		{
			return calendarYear;
		}
		if (policyType == "payroll" && input.PayrollPolicy?.AcademicYearId is Guid payrollYear && payrollYear != Guid.Empty)
		{
			return payrollYear;
		}
		if (policyType == "attendance" && input.AttendancePolicy?.AcademicYearId is Guid attendanceYear && attendanceYear != Guid.Empty)
		{
			return attendanceYear;
		}
		if (!policyId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAcademicYearRequiredForImpact);
		}

		return policyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"payroll" => (await payrollPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"attendance" => (await attendancePolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
		};
	}

	private async Task FillCurrentVersionAsync(HrPolicyImpactPreviewDto preview)
	{
		if (!preview.PolicyId.HasValue)
		{
			return;
		}

		preview.CurrentVersion = preview.PolicyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"payroll" => (await payrollPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"attendance" => (await attendancePolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			_ => null
		};
	}

	private async Task<List<HrPolicyAssignment>> GetAffectedAssignmentsAsync(
		string policyType,
		Guid? policyId,
		Guid academicYearId,
		WorkCalendarPolicyReferences workCalendarRefs)
	{
		var query = (await assignmentRepository.GetQueryableAsync())
			.Where(x => x.AcademicYearId == academicYearId && x.IsActive);

		if (!policyId.HasValue)
		{
			return await asyncExecuter.ToListAsync(query);
		}

		query = policyType switch
		{
			"payroll" => query.Where(x => x.PayrollPolicyId == policyId.Value),
			"attendance" => query.Where(x => x.AttendancePolicyId == policyId.Value),
			"work_calendar" when workCalendarRefs.IsEmpty => query.Where(_ => false),
			"work_calendar" => query.Where(x =>
				workCalendarRefs.PayrollPolicyIds.Contains(x.PayrollPolicyId) ||
				workCalendarRefs.AttendancePolicyIds.Contains(x.AttendancePolicyId)),
			_ => query.Where(_ => false)
		};

		return await asyncExecuter.ToListAsync(query);
	}

	private static HrPolicyAffectedEmployeeDto ToAffectedEmployeeDto(Employee employee) => new()
	{
		EmployeeId = employee.Id,
		EmployeeCode = employee.EmployeeCode,
		EmployeeName = employee.EmployeeName,
		Department = employee.Department,
		RoleId = employee.RoleId,
		ShiftId = employee.ShiftId,
		EmployeeType = employee.EmployeeGroup?.ToString()
	};

}
