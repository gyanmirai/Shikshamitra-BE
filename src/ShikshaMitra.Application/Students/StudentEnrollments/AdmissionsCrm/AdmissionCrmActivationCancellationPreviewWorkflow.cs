using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationCancellationPreviewWorkflow(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AttendanceEntry, Guid> attendanceRepository,
	IRepository<DueFee, Guid> dueFeeRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmActivationCancellationPreviewDto> BuildAsync(AdmissionCrmActivation activation)
	{
		var checks = new List<AdmissionCrmActivationCancellationCheckDto>();
		void Check(string key, bool passed, string detail) => checks.Add(new() { Key = key, Passed = passed, Detail = detail });
		var stateAllowed = activation.Status is AdmissionCrmActivationStatus.FirstDayReady or AdmissionCrmActivationStatus.CancellationPending;
		Check("activation_state", stateAllowed, stateAllowed
			? "Activation is eligible for cancellation review."
			: "Only a first-day-ready activation can enter cancellation review.");
		var query = await cycleRepository.GetQueryableAsync();
		var cycle = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == activation.AcademicYearId));
		var windowDays = cycle?.ActivationCancellationWindowDays ?? 7;
		var windowEndsAt = activation.FirstDayReadyAt?.AddDays(windowDays);
		var windowOpen = windowDays > 0 && windowEndsAt.HasValue && Clock.Now <= windowEndsAt.Value;
		Check("safe_window", windowOpen, windowOpen
			? $"Cancellation window remains open until {windowEndsAt:O}."
			: "The configured same-cycle cancellation window is closed or disabled.");
		var studentExists = activation.StudentId.HasValue;
		Check("student_link", studentExists, studentExists ? "Activated Student is linked." : "Activated Student linkage is missing.");
		if (activation.StudentId.HasValue)
		{
			var attendanceQuery = await attendanceRepository.GetQueryableAsync();
			var hasAttendance = await AsyncExecuter.AnyAsync(attendanceQuery.Where(x =>
				x.TenantId == CurrentTenant.Id && x.StudentId == activation.StudentId.Value));
			Check("attendance_activity", !hasAttendance, hasAttendance
				? "Attendance activity exists; use the Student Records leaving workflow." : "No attendance activity exists.");
			var feeQuery = await dueFeeRepository.GetQueryableAsync();
			var hasFinancialActivity = await AsyncExecuter.AnyAsync(feeQuery.Where(x =>
				x.TenantId == CurrentTenant.Id && x.StudentId == activation.StudentId.Value &&
				(x.PaidAmount > 0 || x.RefundedAmount > 0)));
			Check("finance_activity", !hasFinancialActivity, hasFinancialActivity
				? "Posted payment or refund activity exists; Finance and Student Records must handle the case."
				: "No posted payment or refund activity exists.");
		}
		return new AdmissionCrmActivationCancellationPreviewDto
		{
			ActivationId = activation.Id, CancellationWindowEndsAt = windowEndsAt,
			Checks = checks, CanCancel = checks.All(x => x.Passed)
		};
	}
}
