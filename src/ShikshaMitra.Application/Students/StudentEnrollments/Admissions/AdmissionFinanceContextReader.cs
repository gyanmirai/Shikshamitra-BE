using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionFinanceContextReader(
	AdmissionFeeSelectionAccessService access,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionApplication, Guid> applications,
	IReadOnlyRepository<Standard, Guid> standards,
	IRepository<StudentOnboardingCase, Guid> cases) : ApplicationService, IAdmissionFinanceContextReader, ITransientDependency
{
	public async Task<AdmissionFinanceContext> GetAsync(Guid applicationId)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add))
			throw new AbpAuthorizationException();
		var application = await access.GetApplicationAsync(applicationId);
		var applicant = await applicants.GetAsync(application.ApplicantId);
		var offering = await offerings.GetAsync(application.AdmissionOfferingId);
		var cycle = await cycles.GetAsync(application.AdmissionCycleId);
		if (application.TenantId != CurrentTenant.Id || applicant.TenantId != CurrentTenant.Id ||
			offering.TenantId != CurrentTenant.Id || cycle.TenantId != CurrentTenant.Id || offering.AdmissionCycleId != cycle.Id)
			throw new AbpAuthorizationException();
		var item = application.OnboardingCaseId.HasValue ? await cases.GetAsync(application.OnboardingCaseId.Value) : null;
		if (item != null && (item.ApplicationId != application.Id || item.TenantId != CurrentTenant.Id)) throw new AbpAuthorizationException();
		var selection = application.GetFeeSelection();
		var closed = application.Status is AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.Rejected or AdmissionApplicationStatus.OfferDeclined ||
			item?.Status == StudentOnboardingStatus.Cancelled;
		var blocker = closed ? "This admission journey is closed. Finance can review any existing payments." :
			selection == null ? "Confirm a published fee structure before collecting fees." : null;
		var standard = await standards.GetAsync(offering.StandardId);
		return new(application.Id, applicant.Id, cycle.AcademicYearId, offering.BoardId, offering.MediumId, offering.StageId,
			offering.StandardId, item?.DivisionId, application.StudentId ?? applicant.ExistingStudentId,
			string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			application.ApplicationNumber, standard.Name, application.ConcurrencyStamp, selection?.StructureId, selection?.RevisionNumber,
			selection?.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).ToList() ?? [], blocker == null, blocker, item?.StudentEnrollmentId);
	}

	public async Task RecordPaymentEvidenceAsync(Guid applicationId, Guid accountId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add);
		var application = await access.GetApplicationAsync(applicationId);
		application.RecordFinanceFeePayment(accountId);
		// Rotating the source concurrency token makes a racing fee-selection save fail
		// atomically instead of accepting a payment against a superseded selection.
		await applications.UpdateAsync(application, false);
	}
}
