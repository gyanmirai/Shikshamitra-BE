using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDirectFamilyMatchService(
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionGuardian, Guid> guardians,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionApplication, Guid> applications,
	AdmissionInquiryScopeAccessService inquiryScope,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionOfferingRuntimeService offeringRuntime)
	: ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionDirectFamilyMatchDto>> FindAsync(Guid offeringId, string guardianMobile)
	{
		var offering = (await offeringRuntime.GetAvailableAsync(offeringId)).Offering;
		if (offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed);
		return await FindAccessibleMatchesAsync(AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(guardianMobile));
	}

	public async Task EnsureCanLinkAsync(Guid familyId, string normalizedMobile,
		string errorCode = AdmissionErrorCodes.DirectRegistrationFamilyMismatch)
	{
		var matches = await FindAccessibleMatchesAsync(normalizedMobile);
		if (matches.All(match => match.FamilyId != familyId))
			throw new BusinessException(errorCode,
				"This family is not available in your admission scope for the supplied guardian mobile.");
	}

	private async Task<List<AdmissionDirectFamilyMatchDto>> FindAccessibleMatchesAsync(string phone)
	{
		var matchingGuardians = await guardians.GetListAsync(item => item.IsPrimaryContact && item.Mobile == phone);
		if (matchingGuardians.Count == 0) return [];
		var familyIds = matchingGuardians.Select(item => item.FamilyId).Distinct().ToList();
		var familyApplicants = await applicants.GetListAsync(item => familyIds.Contains(item.FamilyId));
		if (familyApplicants.Count == 0) return [];
		var applicantIds = familyApplicants.Select(item => item.Id).ToList();
		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		var scopedOfferingIds = await AsyncExecuter.ToListAsync(scopedOfferings.Select(item => item.Id));
		var scopedApplicationApplicantIds = await AsyncExecuter.ToListAsync(
			(await applications.GetQueryableAsync()).Where(application =>
				applicantIds.Contains(application.ApplicantId) &&
				scopedOfferingIds.Contains(application.AdmissionOfferingId))
			.Select(application => application.ApplicantId).Distinct());
		var scopedInquiryApplicantIds = await AsyncExecuter.ToListAsync(
			(await inquiryScope.GetScopedQueryAsync()).Where(inquiry =>
				inquiry.ApplicantId.HasValue && applicantIds.Contains(inquiry.ApplicantId.Value))
			.Select(inquiry => inquiry.ApplicantId!.Value).Distinct());
		var visibleApplicantIds = scopedApplicationApplicantIds.Concat(scopedInquiryApplicantIds).ToHashSet();
		var visibleFamilyIds = familyApplicants.Where(item => visibleApplicantIds.Contains(item.Id))
			.Select(item => item.FamilyId).ToHashSet();
		var matchedFamilies = await families.GetListAsync(item =>
			familyIds.Contains(item.Id) && item.Status == AdmissionFamilyStatus.Active);
		return matchedFamilies.Where(family => visibleFamilyIds.Contains(family.Id))
			.Select(family => new AdmissionDirectFamilyMatchDto
			{
				FamilyId = family.Id,
				FamilyNumber = family.FamilyNumber,
				PrimaryGuardianName = matchingGuardians.Where(guardian => guardian.FamilyId == family.Id)
					.Select(guardian => string.Join(" ", new[] { guardian.FirstName, guardian.LastName }
						.Where(value => !string.IsNullOrWhiteSpace(value)))).FirstOrDefault() ?? string.Empty,
				Applicants = familyApplicants.Where(applicant =>
					applicant.FamilyId == family.Id && visibleApplicantIds.Contains(applicant.Id))
					.Select(applicant => string.Join(" ", new[] { applicant.FirstName, applicant.LastName }
						.Where(value => !string.IsNullOrWhiteSpace(value)))).ToList()
			}).ToList();
	}
}
