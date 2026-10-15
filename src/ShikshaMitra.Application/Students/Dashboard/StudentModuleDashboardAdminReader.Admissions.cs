using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardAdminReader
{
	private async Task FillAdmissionsAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardContextDto context, StudentModuleDashboardFilter filter)
	{
		result.Description = "Admissions, decisions and recorded onboarding work in your selected academic scope.";
		var yearCycles = (await cycles.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == filter.YearId);
		var offeringQuery = (await offerings.GetQueryableAsync()).Where(o => o.TenantId == CurrentTenant.Id && yearCycles.Any(c => c.Id == o.AdmissionCycleId) &&
			(!filter.BoardId.HasValue || o.BoardId == filter.BoardId) && (!filter.MediumId.HasValue || o.MediumId == filter.MediumId) && (!filter.StandardId.HasValue || o.StandardId == filter.StandardId));
		if (context.Links.Any(l => l.Id == "inquiries"))
		{
			var query = (await inquiryScope.GetScopedQueryAsync()).Where(i => yearCycles.Any(c => c.Id == i.AdmissionCycleId));
			if (filter.BoardId.HasValue || filter.MediumId.HasValue || filter.StandardId.HasValue) query = query.Where(i => offeringQuery.Any(o => o.Id == i.AdmissionOfferingId));
			if (filter.DivisionId.HasValue)
			{
				var appQuery = await applications.GetQueryableAsync(); var caseQuery = await onboarding.GetScopedCasesAsync();
				query = query.Where(i => appQuery.Any(a => a.InquiryId == i.Id && caseQuery.Any(c => c.ApplicationId == a.Id && c.DivisionId == filter.DivisionId)));
			}
			var reminders = await followUps.GetQueryableAsync(); var now = Clock.Now.ToUniversalTime();
			result.WorkGroups.Add(Metric("inquiryFollowUps", "Inquiry follow-ups due", await AsyncExecuter.CountAsync(query.Where(i => reminders.Any(f =>
				f.TenantId == CurrentTenant.Id && f.InquiryId == i.Id && f.Status == AdmissionInquiryFollowUpStatus.Scheduled && f.ScheduledAt <= now))),
				filter.Link("/student/admissions/inquiries", "inquiry-followups"), P.AdmissionInquiries.View, "warning"));
		}
		if (context.Links.Any(l => l.Id == "applications"))
		{
			var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
			var query = (await applicationScope.GetScopedApplicationsAsync()).Where(a => yearCycles.Any(c => c.Id == a.AdmissionCycleId) &&
				offeringQuery.Any(o => o.Id == a.AdmissionOfferingId) && scopedOfferings.Any(o => o.Id == a.AdmissionOfferingId));
			var reader = LazyServiceProvider.LazyGetRequiredService<AdmissionDashboardQueryService>();
			result.Metrics.Add(Metric("applications", "Active applications", await AsyncExecuter.CountAsync(await reader.ApplyAsync(query, true, false, filter.DivisionId)),
				filter.Link("/student/admissions/applications", "active-applications"), P.AdmissionApplications.View));
			if (await AuthorizationService.IsGrantedAsync(P.AdmissionApplications.Decide))
				result.WorkGroups.Add(Metric("admissionDecisions", "Admission decisions pending", await AsyncExecuter.CountAsync(await reader.ApplyAsync(query, false, true, filter.DivisionId)),
					filter.Link("/student/admissions/applications", "admission-decisions"), P.AdmissionApplications.View, "warning"));
		}
		if (context.Links.Any(l => l.Id == "onboarding")) await FillOnboardingAsync(result, filter);
	}

	private async Task FillOnboardingAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardFilter filter)
	{
		var query = (await onboarding.GetScopedCasesAsync()).Where(c => c.AcademicYearId == filter.YearId &&
			(!filter.BoardId.HasValue || c.IntendedBoardId == filter.BoardId) && (!filter.MediumId.HasValue || c.IntendedMediumId == filter.MediumId) &&
			(!filter.StandardId.HasValue || c.IntendedStandardId == filter.StandardId) && (!filter.DivisionId.HasValue || c.DivisionId == filter.DivisionId));
		var unfinished = await onboarding.FilterDashboardQueueAsync(query, "unfinished");
		var ready = await onboarding.FilterDashboardQueueAsync(query, "ready");
		var attention = await onboarding.FilterDashboardQueueAsync(query, "attention");
		result.AttentionTotalCount = await AsyncExecuter.CountAsync(attention);
		result.Metrics.Add(Metric("setup", "Unfinished onboarding", await AsyncExecuter.CountAsync(unfinished), filter.Link("/student/onboarding/setup", "unfinished"), P.StudentOnboarding.View));
		result.Metrics.Add(Metric("ready", "Ready for activation review", await AsyncExecuter.CountAsync(ready), filter.Link("/student/onboarding/setup", "ready"), P.StudentOnboarding.View, "success"));
		result.Metrics.Add(Metric("attention", "Setup needing attention", result.AttentionTotalCount, filter.Link("/student/onboarding/setup", "attention"), P.StudentOnboarding.View, "warning"));
		foreach (var task in new[] { ("division", "Division assignment"), ("gr-number", "GR number"), ("roll-number", "Roll number"),
			("documents", "Required documents"), ("credentials", "App access and credentials"), ("ready", "Activation review") })
		{
			var taskQuery = await onboarding.FilterDashboardQueueAsync(query, task.Item1);
			var id = task.Item1 switch { "gr-number" => "gr", "roll-number" => "roll", _ => task.Item1 };
			result.OnboardingTasks.Add(Metric(id, task.Item2, await AsyncExecuter.CountAsync(taskQuery), filter.Link("/student/onboarding/setup", task.Item1), P.StudentOnboarding.View, task.Item1 == "ready" ? "success" : "warning"));
		}
		var selected = attention.OrderBy(c => c.DueAt).ThenBy(c => c.Id).Take(4);
		var standards = await LazyServiceProvider.LazyGetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync();
		var divisions = await LazyServiceProvider.LazyGetRequiredService<IRepository<Division, Guid>>().GetQueryableAsync();
		var users = await LazyServiceProvider.LazyGetRequiredService<IRepository<IdentityUser, Guid>>().GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(from item in selected
			join student in await students.GetQueryableWithoutAutoIncludesAsync() on item.StudentId equals student.Id
			join standard in standards on item.IntendedStandardId equals standard.Id
			join division in divisions on item.DivisionId equals (Guid?)division.Id into divisionRows from division in divisionRows.DefaultIfEmpty()
			join owner in users on item.AssignedToUserId equals (Guid?)owner.Id into ownerRows from owner in ownerRows.DefaultIfEmpty()
			select new { item.Id, item.StudentId, item.DueAt, item.LastError, item.StudentEnrollmentId, item.ExtraProperties, First = student.FirstName, Last = student.LastName,
				ClassName = standard.Name, DivisionName = division == null ? null : division.Name,
				OwnerName = owner == null ? null : ((owner.Name == null || owner.Name == "") && (owner.Surname == null || owner.Surname == "") ? owner.UserName : ((owner.Name ?? "") + " " + (owner.Surname ?? "")).Trim()) });
		var ids = rows.Select(r => r.Id).ToArray();
		var steps = await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentOnboardingStep, Guid>>().GetListAsync(s => Enumerable.Contains(ids, s.OnboardingCaseId) &&
			s.Required && (s.Status == StudentOnboardingStepStatus.Pending || s.Status == StudentOnboardingStepStatus.Blocked || s.Status == StudentOnboardingStepStatus.InProgress));
		var handoffs = await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().GetListAsync(h =>
			Enumerable.Contains(ids, h.OnboardingCaseId) && h.Required && h.ServiceKey != "finance-first-payment");
		result.AttentionItems = rows.Select(r => new StudentModuleDashboardAttentionDto {
			CaseId = r.Id, StudentId = r.StudentId, StudentName = $"{r.First} {r.Last}".Trim(), ClassName = r.ClassName,
			DivisionName = r.DivisionName, OwnerName = r.OwnerName, DueAt = r.DueAt,
			Blocker = StudentOnboardingWorkSummary.PrimaryBlocker(steps.Where(s => s.OnboardingCaseId == r.Id), handoffs.Where(h => r.StudentEnrollmentId.HasValue && h.OnboardingCaseId == r.Id && (h.ServiceKey != "service-consent" || !r.ExtraProperties.ContainsKey("ReadinessRulesSnapshot")))) ?? r.LastError ?? "Setup overdue",
			Href = filter.Link($"/student/onboarding/setup?case={r.Id:D}", "attention") }).ToList();
	}
}
