using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task EnsurePerformanceOutcomeAsync(Guid assignmentId, decimal score, string finalRating, int index)
	{
		var outcomeQuery = await _performanceOutcomeRepository.GetQueryableAsync();
		if (outcomeQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		await _performanceOutcomeRepository.InsertAsync(
			new PerformanceOutcome(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				assignmentId,
				score,
				finalRating,
				"Strong classroom ownership, timely documentation, and steady collaboration.",
				"Continue improving data-backed interventions and peer mentoring.",
				index % 2 == 0 ? PerformanceRecommendationType.Training : PerformanceRecommendationType.Increment,
				"Recommendation only; payroll is not updated automatically.",
				index % 2 == 1,
				index % 5 == 0),
			autoSave: true);
	}

	private async Task EnsureClassroomObservationAsync(Guid assignmentId, Guid employeeId, int index)
	{
		var observationQuery = await _classroomObservationRepository.GetQueryableAsync();
		if (observationQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		await _classroomObservationRepository.InsertAsync(
			new ClassroomObservation(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				assignmentId,
				employeeId,
				_clock.Now.Date.AddDays(-index - 3),
				$"Class {6 + index % 5}",
				index % 2 == 0 ? "Mathematics" : "English",
				4,
				3 + index % 3,
				4,
				4,
				"Lesson flow was structured with visible student checks for understanding."),
			autoSave: true);
	}

	private async Task EnsurePerformanceEvidenceAsync(Guid assignmentId, Guid criterionId, int index)
	{
		var evidenceQuery = await _performanceEvidenceRepository.GetQueryableAsync();
		if (evidenceQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		await _performanceEvidenceRepository.InsertAsync(
			new PerformanceEvidence(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				assignmentId,
				criterionId,
				"Lesson plan and improvement tracker",
				$"performance-evidence-{index + 1}.pdf",
				null,
				"Seeded evidence metadata for demo review inspection."),
			autoSave: true);
	}

	private async Task EnsurePerformanceFeedbackAsync(Guid assignmentId, Employee employee, int index)
	{
		var feedbackQuery = await _performanceFeedbackRepository.GetQueryableAsync();
		if (feedbackQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		await _performanceFeedbackRepository.InsertAsync(
			new PerformanceFeedback(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				assignmentId,
				index % 2 == 0 ? PerformanceFeedbackSource.Parent : PerformanceFeedbackSource.InternalStakeholder,
				index % 2 == 0 ? "Parent feedback summary" : "Department coordinator",
				$"{employee.EmployeeName} is responsive, prepared, and consistent in follow-up."),
			autoSave: true);
	}

	private async Task EnsureDevelopmentPlanAsync(Guid assignmentId, Guid employeeId, int index)
	{
		var planQuery = await _developmentPlanRepository.GetQueryableAsync();
		if (planQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		var plan = new DevelopmentPlan(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			assignmentId,
			index % 3 == 0 ? "Strengthen differentiated instruction" : "Improve evidence-backed reporting",
			"Attend a peer observation cycle and submit one implementation reflection.",
			employeeId,
			_clock.Now.Date.AddDays(45 + index));
		plan.Update(plan.Goal, plan.ActionPlan, plan.OwnerEmployeeId, plan.TargetDate, DevelopmentPlanStatus.InProgress);
		await _developmentPlanRepository.InsertAsync(plan, autoSave: true);
	}

	private async Task EnsurePerformanceAuditAsync(Guid assignmentId, int index)
	{
		var auditQuery = await _performanceAuditRepository.GetQueryableAsync();
		if (auditQuery.Any(x => x.ReviewAssignmentId == assignmentId))
		{
			return;
		}

		await _performanceAuditRepository.InsertAsync(
			new PerformanceAuditEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				assignmentId,
				"SeededDemoReview",
				$"Demo performance review seeded with workflow state {index % 4}.",
				"{\"source\":\"HRSeeder\"}"),
			autoSave: true);
	}
}
