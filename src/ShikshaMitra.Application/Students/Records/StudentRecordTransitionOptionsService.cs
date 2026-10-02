using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordTransitionOptionsService(IUserScopeAccessService scopes, StudentRecordsAccessService access,
	IRepository<AcademicYear, Guid> years, IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<AdmissionOffering, Guid> offerings, IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionApplication, Guid> applications, IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<StudentOnboardingCase, Guid> onboardingCases,
	StudentRecordsPolicyResolver policies, StudentOnboardingRollRuleReader rollRules) : ApplicationService, ITransientDependency
{
	public async Task<StudentRecordTransitionOptionsDto> GetAsync(Guid? yearId, Guid? studentId, Guid? admissionOfferingId = null)
	{
		var scope = await scopes.GetCurrentUserScopeAsync();
		var fullScope = scope.IsAdmin || scope.HasTenantWideScope();
		var result = new StudentRecordTransitionOptionsDto();
		if (!CurrentTenant.Id.HasValue || (!fullScope && !scope.HasAnyAccess)) return result;
		var policyYear = studentId.HasValue ? (await access.GetScopedStudentAsync(studentId.Value)).AcademicYearId : yearId;
		var policy = (await policies.ResolveAsync(policyYear)).Policy.Transitions;
		result.ReturnPolicy = new() { AllowedRoutes = policy.AllowedReturnRoutes, DefaultRoute = policy.DefaultReturnRoute };
		var yearQuery = (await years.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		if (!fullScope) yearQuery = yearQuery.Where(x => scope.AcademicYearIds.Contains(x.Id));
		result.AcademicYears = await AsyncExecuter.ToListAsync(yearQuery.OrderByDescending(x => x.StartDate).Select(x => new StudentRecordTransitionOptionDto { Id = x.Id, Name = x.Name }));
		if (!yearId.HasValue) return result;
		result.RollAssignmentMode = await rollRules.GetAssignmentModeAsync(yearId.Value);
		var standardQuery = (await standards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == yearId);
		var divisionQuery = (await divisions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		var allDivisions = divisionQuery;
		if (!fullScope)
		{
			standardQuery = standardQuery.Where(x => scope.AcademicYearIds.Contains(yearId.Value) && (scope.WildcardBoardAcademicYearIds.Contains(yearId.Value) ||
				scope.WildcardMediumBoardIds.Contains(x.EducationMediumStage.EducationMedium.EducationBoardId) || scope.WildcardMediumStageMediumIds.Contains(x.EducationMediumStage.EducationMediumId) ||
				scope.WildcardStandardMediumStageIds.Contains(x.EducationMediumStageId) || scope.WildcardDivisionStandardIds.Contains(x.Id) || allDivisions.Any(d => d.StandardId == x.Id && scope.DivisionIds.Contains(d.Id))));
			divisionQuery = divisionQuery.Where(x => scope.WildcardBoardAcademicYearIds.Contains(yearId.Value) || scope.WildcardDivisionStandardIds.Contains(x.StandardId) || scope.DivisionIds.Contains(x.Id) ||
				standardQuery.Any(s => s.Id == x.StandardId && (scope.WildcardMediumBoardIds.Contains(s.EducationMediumStage.EducationMedium.EducationBoardId) || scope.WildcardMediumStageMediumIds.Contains(s.EducationMediumStage.EducationMediumId) || scope.WildcardStandardMediumStageIds.Contains(s.EducationMediumStageId))));
		}
		result.Standards = await AsyncExecuter.ToListAsync(standardQuery.OrderBy(x => x.Name).Select(x => new StudentRecordTransitionOptionDto { Id = x.Id, Name = x.Name, ParentId = x.EducationMediumStageId }));
		var standardIds = result.Standards.Select(x => x.Id).ToArray();
		result.Divisions = await AsyncExecuter.ToListAsync(divisionQuery.Where(x => Enumerable.Contains(standardIds, x.StandardId)).OrderBy(x => x.Name).Select(x => new StudentRecordTransitionOptionDto { Id = x.Id, Name = x.Name, ParentId = x.StandardId }));
		var cycleQuery = await cycles.GetQueryableAsync();
		result.Offerings = await AsyncExecuter.ToListAsync((await offerings.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.IsActive && Enumerable.Contains(standardIds, x.StandardId) &&
			cycleQuery.Any(c => c.Id == x.AdmissionCycleId && c.TenantId == CurrentTenant.Id && c.AcademicYearId == yearId)).Select(x => new StudentRecordTransitionOptionDto { Id = x.Id, Name = x.Name, ParentId = x.StandardId, EntryMode = x.EntryMode }));
		if (studentId.HasValue)
		{
			await access.GetScopedStudentAsync(studentId.Value);
			var applicantQuery = await applicants.GetQueryableAsync();
			result.FreshApplicationRequiresRegistration = await LazyServiceProvider.LazyGetRequiredService<StudentRecordAdmissionReturnService>().RequiresRegistrationAsync(studentId.Value);
			if (await AuthorizationService.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start))
			{
				var caseQuery = await onboardingCases.GetQueryableAsync();
				var offeringIds = result.Offerings.Select(x => x.Id).ToArray();
				result.ResumeApplications = await AsyncExecuter.ToListAsync((await applications.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
					Enumerable.Contains(offeringIds, x.AdmissionOfferingId) && !x.StudentId.HasValue && !x.OnboardingCaseId.HasValue &&
					!caseQuery.Any(c => c.TenantId == CurrentTenant.Id && c.ApplicationId == x.Id) &&
					applicantQuery.Any(a => a.Id == x.ApplicantId && a.TenantId == CurrentTenant.Id && a.ExistingStudentId == studentId) &&
					cycleQuery.Any(c => c.Id == x.AdmissionCycleId && c.TenantId == CurrentTenant.Id && c.AcademicYearId == yearId) &&
					x.Status != AdmissionApplicationStatus.AdmissionFinalized && x.Status != AdmissionApplicationStatus.Enrolled && x.Status != AdmissionApplicationStatus.Rejected && x.Status != AdmissionApplicationStatus.Withdrawn && x.Status != AdmissionApplicationStatus.OfferDeclined)
					.Select(x => new StudentRecordTransitionOptionDto { Id = x.Id, Name = x.ApplicationNumber, ParentId = x.AdmissionOfferingId }));
			}
		}
		if (admissionOfferingId.HasValue)
		{
			var selected = result.Offerings.SingleOrDefault(x => x.Id == admissionOfferingId.Value) ?? throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
			if (selected.EntryMode != AdmissionEntryMode.DirectRegistration && await AuthorizationService.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create))
				selected.InquiryIntake = await LazyServiceProvider.LazyGetRequiredService<StudentRecordAdmissionReturnService>().GetIntakeAsync(selected.Id, studentId);
		}
		return result;
	}
}
