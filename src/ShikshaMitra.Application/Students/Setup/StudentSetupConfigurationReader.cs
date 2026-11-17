using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records.HouseManagement;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Setup;

/// <summary>Advisory effective configuration presence; never a readiness or publication command.</summary>
[RemoteService(false)]
public class StudentSetupConfigurationReader(
	IRepository<AdmissionCycle, Guid> periods, IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionWorkflowDefinition, Guid> workflows, IRepository<AdmissionPublicInquiryLink, Guid> links,
	AdmissionApplicationScopeAccessService admissionScope, HouseManagementAccessService houseAccess,
	IRepository<HouseSettings, Guid> houses, StudentRecordsPolicyResolver recordsPolicy,
	StudentScopeAccessService studentScope, IRepository<StudentOnboardingSettings, Guid> onboardingSettings)
	: ApplicationService
{
	public async Task ReadAsync(List<StudentSetupSettingStateDto> settings, StudentSetupAccess.Context context)
	{
		foreach (var setting in settings)
		{
			if (!setting.CanView) continue;
			if (setting.Key is "admission-periods" or "gr-number" or "roll-number" or "records-policy")
				setting.CanManage &= context.CanManageYear;
			if (setting.Key is "houses" or "admission-offerings" or "public-inquiry-links" && !context.MediumId.HasValue ||
				setting.Key is "gr-number" or "roll-number" && !context.StandardId.HasValue)
			{
				setting.ConfigurationState = "context-required";
				setting.CanManage = false;
				continue;
			}
			var configured = setting.Key switch {
				"admission-periods" => await periods.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == context.Year.Id && x.Status == AdmissionCycleStatus.Published),
				"admission-forms" => await forms.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.Status == AdmissionFormStatus.Published &&
					(x.Purpose == AdmissionFormPurpose.Inquiry || x.Purpose == AdmissionFormPurpose.Registration)),
				"admission-stages" => await workflows.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.Status == AdmissionWorkflowStatus.Published),
				"admission-offerings" => await OfferingAsync(context),
				"public-inquiry-links" => await PublicLinkAsync(context),
				"houses" => await HouseAsync(context, setting),
				"records-policy" => await PolicyAsync(context, setting),
				"gr-number" or "roll-number" => await NumberingAsync(context, setting),
				_ => false
			};
			setting.ConfigurationState = configured ? "configured" : "empty";
		}
	}

	private async Task<bool> OfferingAsync(StudentSetupAccess.Context context)
	{
		var query = await ScopedOfferingsAsync(context);
		return await AsyncExecuter.AnyAsync(query.Where(x => x.IsActive));
	}

	private async Task<IQueryable<AdmissionOffering>> ScopedOfferingsAsync(StudentSetupAccess.Context context)
	{
		var cycles = (await periods.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == context.Year.Id && x.Status == AdmissionCycleStatus.Published);
		return (await admissionScope.GetScopedOfferingsAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.MediumId == context.MediumId && (!context.StandardId.HasValue || x.StandardId == context.StandardId) &&
			cycles.Any(c => c.Id == x.AdmissionCycleId));
	}

	private async Task<bool> PublicLinkAsync(StudentSetupAccess.Context context)
	{
		var offerings = await ScopedOfferingsAsync(context);
		var publishedForms = (await forms.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.Purpose == AdmissionFormPurpose.Inquiry && x.Status == AdmissionFormStatus.Published);
		var now = Clock.Now.ToUniversalTime();
		return await AsyncExecuter.AnyAsync((await links.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.IsActive &&
			(!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now) && publishedForms.Any(f => f.Id == x.InquiryFormDefinitionId) &&
			offerings.Any(o => o.AdmissionCycleId == x.AdmissionCycleId && o.InquiryFormDefinitionId == x.InquiryFormDefinitionId && o.IsActive)));
	}

	private async Task<bool> HouseAsync(StudentSetupAccess.Context context, StudentSetupSettingStateDto setting)
	{
		var access = await houseAccess.GetAsync(context.MediumId!.Value);
		setting.CanManage &= access.CanManage;
		return await houses.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicMediumId == context.MediumId);
	}

	private async Task<bool> PolicyAsync(StudentSetupAccess.Context context, StudentSetupSettingStateDto setting)
	{
		await studentScope.EnsureSettingsScopeAsync(context.Year.Id, false, "StudentSetupRecordsPolicy");
		var snapshot = await recordsPolicy.ResolveAsync(context.Year.Id);
		setting.ConfigurationSource = snapshot.IsStarter ? "school-default" : snapshot.ScopeAcademicYearId.HasValue ? "academic-year" : "school";
		return true;
	}

	private async Task<bool> NumberingAsync(StudentSetupAccess.Context context, StudentSetupSettingStateDto setting)
	{
		await studentScope.EnsureCanAccessAcademicYearAsync(context.Year.Id, "StudentSetupNumbering");
		// Select the same year override as the runtime owner; a year draft does not fall back to a school rule.
		var saved = await AsyncExecuter.FirstOrDefaultAsync((await onboardingSettings.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && (x.AcademicYearId == context.Year.Id || x.AcademicYearId == null))
			.OrderByDescending(x => x.AcademicYearId.HasValue).Select(x => new { x.AcademicYearId, x.ConfigurationJson }));
		if (saved == null) return false;
		var configuration = new OnboardingRuleConfiguration(saved.ConfigurationJson);
		var section = setting.Key == "gr-number" ? "grNumber" : "rollNumber";
		var rule = configuration.ResolveRule(section, context.BoardId!.Value, context.MediumId!.Value,
			context.StageId!.Value, context.StandardId!.Value, null, null, AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now));
		if (!rule.HasValue) return false;
		setting.ConfigurationSource = saved.AcademicYearId.HasValue ? "academic-year" : "school";
		return true;
	}
}
