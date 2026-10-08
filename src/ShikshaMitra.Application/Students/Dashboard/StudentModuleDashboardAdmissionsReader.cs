using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

[RemoteService(false)]
public class StudentModuleDashboardAdmissionsReader(AdmissionInquiryScopeAccessService inquiryScope,
	AdmissionApplicationScopeAccessService applicationScope, IUserScopeAccessService scopes,
	IRepository<AdmissionCycle, Guid> cycles, IRepository<AdmissionApplication, Guid> applications,
	IRepository<StudentOnboardingCase, Guid> cases) : ApplicationService
{
	public async Task FillAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardContextDto context)
	{
		var schoolOverview = context.Mode is "admin" or "leadership";
		result.Description = schoolOverview ? "Admissions and onboarding across your permitted academic scope." : "Your assigned inquiries and setup cases, plus applications visible in your academic scope.";
		if (context.Links.Any(link => link.Id == "inquiries"))
		{
			await AuthorizationService.CheckAsync(P.AdmissionInquiries.View);
			var yearCycles = (await cycles.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == result.AcademicYearId);
			var inquiries = (await inquiryScope.GetScopedQueryAsync()).Where(i => i.TenantId == CurrentTenant.Id && yearCycles.Any(c => c.Id == i.AdmissionCycleId));
			if (!schoolOverview) inquiries = inquiries.Where(i => i.AssignedCounsellorId == CurrentUser.Id);
			var count = await AsyncExecuter.CountAsync(inquiries.Where(i => i.Status == AdmissionInquiryStatus.New || i.Status == AdmissionInquiryStatus.Contacted || i.Status == AdmissionInquiryStatus.Interested || i.Status == AdmissionInquiryStatus.FollowUp));
			Add(result, "inquiries", schoolOverview ? "Open inquiries" : "My open inquiries", count,
				schoolOverview ? "/student/admissions/inquiries" : "/student/admissions/inquiries?owner=__mine__", P.AdmissionInquiries.View);
		}
		if (context.Links.Any(link => link.Id == "applications"))
		{
			await AuthorizationService.CheckAsync(P.AdmissionApplications.View);
			var yearCycles = (await cycles.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == result.AcademicYearId);
			var offerings = (await applicationScope.GetScopedOfferingsAsync()).Where(o => yearCycles.Any(c => c.Id == o.AdmissionCycleId));
			var query = (await applications.GetQueryableAsync()).Where(a => a.TenantId == CurrentTenant.Id && offerings.Any(o => o.Id == a.AdmissionOfferingId));
			var counts = await AsyncExecuter.ToListAsync(query.GroupBy(a => a.Status).Select(group => new { Status = group.Key, Count = group.Count() }));
			Add(result, "applications", "Active applications", counts.Where(row => row.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted).Sum(row => row.Count), "/student/admissions/applications", P.AdmissionApplications.View);
		}
		if (context.Links.Any(link => link.Id == "onboarding")) await FillOnboardingAsync(result, schoolOverview);
	}

	private async Task FillOnboardingAsync(StudentModuleDashboardSectionDto result, bool schoolOverview)
	{
		await AuthorizationService.CheckAsync(P.StudentOnboarding.View);
		var scope = await scopes.GetCurrentUserScopeAsync();
		var query = (await cases.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == result.AcademicYearId);
		if (!scope.HasTenantWideScope()) query = query.Where(c => scope.AcademicYearIds.Contains(c.AcademicYearId) &&
			(scope.WildcardBoardAcademicYearIds.Contains(c.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(c.IntendedBoardId) ||
			 scope.WildcardMediumStageMediumIds.Contains(c.IntendedMediumId) || scope.WildcardStandardMediumStageIds.Contains(c.IntendedStageId) ||
			 scope.WildcardDivisionStandardIds.Contains(c.IntendedStandardId) || c.DivisionId.HasValue && scope.DivisionIds.Contains(c.DivisionId.Value)));
		if (!schoolOverview) query = query.Where(c => c.AssignedToUserId == CurrentUser.Id);
		var now = Clock.Now;
		var counts = await AsyncExecuter.FirstOrDefaultAsync(query.GroupBy(_ => 1).Select(group => new
		{
			Active = group.Count(c => c.Status != StudentOnboardingStatus.Completed && c.Status != StudentOnboardingStatus.Cancelled),
			Ready = group.Count(c => c.Status == StudentOnboardingStatus.ReadyForActivation),
			Attention = group.Count(c => c.Status == StudentOnboardingStatus.NeedsAttention || c.Status != StudentOnboardingStatus.Completed && c.Status != StudentOnboardingStatus.Cancelled && c.DueAt < now)
		}));
		var prefix = schoolOverview ? "" : "My ";
		Add(result, "setup", prefix + "unfinished setup cases", counts?.Active ?? 0, "/student/onboarding/setup", P.StudentOnboarding.View);
		Add(result, "ready", prefix + "ready for activation", counts?.Ready ?? 0, "/student/onboarding/setup", P.StudentOnboarding.View, "success");
		Add(result, "attention", prefix + "setup needing attention", counts?.Attention ?? 0, "/student/onboarding/setup", P.StudentOnboarding.View, "warning");
	}

	private static void Add(StudentModuleDashboardSectionDto result, string id, string label, int count, string href, string permission, string tone = "neutral") =>
		result.Metrics.Add(new() { Id = id, Label = label, Value = count, Href = href, Permission = permission, Tone = count == 0 ? "neutral" : tone });
}
