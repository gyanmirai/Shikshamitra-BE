using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public sealed class DashboardSummarySlicePipeline : ITransientDependency
{
	public DashboardSummarySlicePipeline(
		DashboardStudentRecordsSnapshotSliceProvider studentRecordsSnapshotSliceProvider,
		DashboardStudentAttendanceSliceProvider studentAttendanceSliceProvider,
		DashboardActionQueueSliceProvider actionQueueSliceProvider,
		DashboardFinanceHrSnapshotSliceProvider financeHrSnapshotSliceProvider,
		DashboardCalendarSliceProvider calendarSliceProvider,
		DashboardAcademicCurriculumSliceProvider academicCurriculumSliceProvider,
		DashboardAcademicExamWorkflowSliceProvider academicExamWorkflowSliceProvider,
		DashboardComplianceReadinessSliceProvider complianceReadinessSliceProvider,
		DashboardPersonalizationSliceProvider personalizationSliceProvider,
		DashboardTeacherExamEvaluationSliceProvider teacherExamEvaluationSliceProvider,
		DashboardTeacherLearningTaskWorkloadSliceProvider teacherLearningTaskWorkloadSliceProvider)
		: this(
			[
				new("student-records-snapshot", 10, Kpis, "Student records snapshot could not be loaded.", studentRecordsSnapshotSliceProvider),
				new("student-attendance", 20, Kpis, "Attendance data could not be loaded.", studentAttendanceSliceProvider),
				new("action-queue", 30, Actions, "Dashboard actions could not be loaded.", actionQueueSliceProvider),
				new("finance-hr-snapshot", 40, Kpis, "Finance and HR dashboard data could not be loaded.", financeHrSnapshotSliceProvider),
				new("calendar", 50, Calendar, "Calendar items could not be loaded.", calendarSliceProvider),
				new("academic-curriculum", 60, Kpis, "Academic curriculum snapshot could not be loaded.", academicCurriculumSliceProvider),
				new("academic-exam-workflow", 65, Modules, "Academic exam workflow snapshot could not be loaded.", academicExamWorkflowSliceProvider),
				new("compliance-readiness", 70, Kpis, "Compliance readiness could not be loaded.", complianceReadinessSliceProvider),
				new("personalization", 80, Kpis, "Dashboard personalization could not be loaded.", personalizationSliceProvider),
				new("teacher-exam-evaluation", 85, Kpis, "Teacher exam evaluation could not be loaded.", teacherExamEvaluationSliceProvider),
				new("teacher-learning-task-workload", 90, Kpis, "Teacher learning-task workload could not be loaded.", teacherLearningTaskWorkloadSliceProvider)
			])
	{
	}

	private DashboardSummarySlicePipeline(IEnumerable<DashboardSummarySliceRegistration> registrations)
	{
		Registrations = registrations
			.OrderBy(registration => registration.Order)
			.ThenBy(registration => registration.Name)
			.ToList();
	}

	public IReadOnlyList<DashboardSummarySliceRegistration> Registrations { get; }

	public static DashboardSummarySlicePipeline CreateForTesting(
		IEnumerable<DashboardSummarySliceRegistration> registrations) =>
		new(registrations);
}
