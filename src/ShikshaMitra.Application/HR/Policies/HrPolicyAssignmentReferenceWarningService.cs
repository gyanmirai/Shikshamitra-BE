using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentReferenceWarningService(
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository)
	: ITransientDependency
{
	public async Task<bool> AddReferenceWarningsAsync(
		HrPolicyImpactPreviewDto preview,
		CreateUpdateHrPolicyAssignmentDto assignment)
	{
		var isUsable = true;
		var attendancePolicy = await attendancePolicyRepository.FindAsync(assignment.AttendancePolicyId);
		var payrollPolicy = await payrollPolicyRepository.FindAsync(assignment.PayrollPolicyId);

		if (!await AddAttendancePolicyWarningsAsync(preview, assignment, attendancePolicy))
		{
			isUsable = false;
		}

		if (!await AddPayrollPolicyWarningsAsync(preview, assignment, payrollPolicy))
		{
			isUsable = false;
		}

		return isUsable;
	}

	private async Task<bool> AddAttendancePolicyWarningsAsync(
		HrPolicyImpactPreviewDto preview,
		CreateUpdateHrPolicyAssignmentDto assignment,
		AttendancePolicy? attendancePolicy)
	{
		if (attendancePolicy == null)
		{
			AddBlocking(preview, HRErrorCodes.HrPolicyMustBeActive, "The selected attendance policy was not found.");
			return false;
		}

		if (attendancePolicy.AcademicYearId != assignment.AcademicYearId || attendancePolicy.Status != HrPolicyStatus.Active)
		{
			AddBlocking(
				preview,
				attendancePolicy.AcademicYearId != assignment.AcademicYearId
					? HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch
					: HRErrorCodes.HrPolicyMustBeActive,
				attendancePolicy.AcademicYearId != assignment.AcademicYearId
					? $"Attendance policy \"{attendancePolicy.Name}\" belongs to a different academic year."
					: $"Attendance policy \"{attendancePolicy.Name}\" is {attendancePolicy.Status.ToString().ToLowerInvariant()}. Assign only active policies.");
			return false;
		}

		return await AddWorkCalendarWarningAsync(
			preview,
			attendancePolicy.WorkCalendarPolicyId,
			attendancePolicy.AcademicYearId,
			$"Attendance policy \"{attendancePolicy.Name}\"");
	}

	private async Task<bool> AddPayrollPolicyWarningsAsync(
		HrPolicyImpactPreviewDto preview,
		CreateUpdateHrPolicyAssignmentDto assignment,
		PayrollPolicy? payrollPolicy)
	{
		if (payrollPolicy == null)
		{
			AddBlocking(preview, HRErrorCodes.HrPolicyMustBeActive, "The selected payroll policy was not found.");
			return false;
		}

		if (payrollPolicy.AcademicYearId != assignment.AcademicYearId || payrollPolicy.Status != HrPolicyStatus.Active)
		{
			AddBlocking(
				preview,
				payrollPolicy.AcademicYearId != assignment.AcademicYearId
					? HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch
					: HRErrorCodes.HrPolicyMustBeActive,
				payrollPolicy.AcademicYearId != assignment.AcademicYearId
					? $"Payroll policy \"{payrollPolicy.Name}\" belongs to a different academic year."
					: $"Payroll policy \"{payrollPolicy.Name}\" is {payrollPolicy.Status.ToString().ToLowerInvariant()}. Assign only active policies.");
			return false;
		}

		return await AddWorkCalendarWarningAsync(
			preview,
			payrollPolicy.WorkCalendarPolicyId,
			payrollPolicy.AcademicYearId,
			$"Payroll policy \"{payrollPolicy.Name}\"");
	}

	private async Task<bool> AddWorkCalendarWarningAsync(
		HrPolicyImpactPreviewDto preview,
		Guid workCalendarPolicyId,
		Guid academicYearId,
		string policyLabel)
	{
		var workCalendar = await workCalendarPolicyRepository.FindAsync(workCalendarPolicyId);
		if (workCalendar == null || workCalendar.AcademicYearId != academicYearId)
		{
			AddBlocking(
				preview,
				HRErrorCodes.HrPolicyInvalidWorkCalendar,
				$"{policyLabel} references a missing or mismatched work calendar.");
			return false;
		}

		if (workCalendar.Status == HrPolicyStatus.Active)
		{
			return true;
		}

		AddBlocking(
			preview,
			HRErrorCodes.WorkCalendarPolicyMustBeActive,
			$"{policyLabel} references work calendar \"{workCalendar.Name}\", which is {workCalendar.Status.ToString().ToLowerInvariant()}. Assign policies only after their calendars are active.");
		return false;
	}

	private static void AddBlocking(HrPolicyImpactPreviewDto preview, string code, string message)
	{
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = code,
			Message = message
		});
	}
}
