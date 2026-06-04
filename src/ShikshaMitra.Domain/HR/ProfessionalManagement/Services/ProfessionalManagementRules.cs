using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.ProfessionalManagement;

public static class ProfessionalManagementRules
{
	public static int CountOverdueReviews(
		IReadOnlyCollection<ProfessionalReviewCycle> cycles,
		IReadOnlyCollection<PerformanceReviewAssignment> assignments,
		IReadOnlyCollection<PerformanceReviewStage> stages,
		DateTime today)
	{
		var cycleById = cycles.ToDictionary(x => x.Id);
		var stageByReview = stages.GroupBy(x => x.ReviewAssignmentId).ToDictionary(x => x.Key, x => x.ToList());
		var count = 0;
		foreach (var assignment in assignments)
		{
			if (!cycleById.TryGetValue(assignment.CycleId, out var cycle) ||
			    !stageByReview.TryGetValue(assignment.Id, out var reviewStages))
			{
				continue;
			}

			count += reviewStages.Count(stage =>
			{
				var dueDate = stage.StageType switch
				{
					PerformanceReviewStageType.SelfAppraisal => cycle.SelfReviewDueDate,
					PerformanceReviewStageType.HrVerification or PerformanceReviewStageType.PrincipalManagement => cycle.FinalizationDueDate,
					_ => cycle.ReviewerDueDate
				};
				return dueDate.HasValue && dueDate.Value.Date < today;
			});
		}

		return count;
	}

	public static Guid? ResolveStageReviewer(PerformanceReviewAssignment assignment, PerformanceReviewStageType stageType) => stageType switch
	{
		PerformanceReviewStageType.ReportingManager => assignment.ReportingManagerEmployeeId,
		PerformanceReviewStageType.HodCoordinator => assignment.HodEmployeeId,
		PerformanceReviewStageType.PrincipalManagement => assignment.PrincipalEmployeeId,
		_ => null
	};

	public static bool HasValidRubric(
		IReadOnlyCollection<PerformanceTemplateSection> sections,
		IReadOnlyCollection<PerformanceTemplateCriterion> criteria)
	{
		if (sections.Count == 0 || criteria.Count == 0)
		{
			return false;
		}

		return sections.All(section =>
		{
			var sectionCriteria = criteria.Where(criterion => criterion.SectionId == section.Id).ToList();
			return sectionCriteria.Count > 0 && sectionCriteria.Sum(criterion => criterion.Weight) > 0;
		});
	}

	public static PerformanceStaffCategory InferStaffCategory(Employee employee)
	{
		var text = $"{employee.Designation} {employee.Department} {employee.Role?.Name}".ToLowerInvariant();
		if (text.Contains("class teacher")) return PerformanceStaffCategory.ClassTeacher;
		if (text.Contains("hod") || text.Contains("coordinator") || text.Contains("head of department")) return PerformanceStaffCategory.HodCoordinator;
		if (text.Contains("principal") || text.Contains("vice principal")) return PerformanceStaffCategory.PrincipalVicePrincipal;
		if (text.Contains("account")) return PerformanceStaffCategory.Accounts;
		if (text.Contains("librarian") || text.Contains("lab")) return PerformanceStaffCategory.LibrarianLabAssistant;
		if (text.Contains("transport") || text.Contains("driver") || text.Contains("conductor")) return PerformanceStaffCategory.Transport;
		if (text.Contains("security") || text.Contains("housekeeping")) return PerformanceStaffCategory.SecurityHousekeeping;
		if (text.Contains("it") || text.Contains("support")) return PerformanceStaffCategory.ItSupport;
		if (text.Contains("teacher") || text.Contains("faculty") || text.Contains("academic")) return PerformanceStaffCategory.Teacher;
		return PerformanceStaffCategory.Admin;
	}
}
