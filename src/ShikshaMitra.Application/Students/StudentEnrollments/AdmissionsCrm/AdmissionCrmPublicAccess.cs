using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionApplicantStatusAccess, Guid> statusAccessRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository) : ApplicationService, ITransientDependency
{
	public async Task<(AdmissionApplicantStatusAccess Access, AdmissionInquiryLead Lead)> GetAsync(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		var tokenHash = AdmissionCrmPublicTokenService.HashSecret(token);
		var queryable = await statusAccessRepository.GetQueryableAsync();
		var access = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.TokenHash == tokenHash));
		if (access == null)
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		return (access, await GetLeadAsync(access));
	}

	public async Task<(AdmissionApplicantStatusAccess Access, AdmissionInquiryLead Lead)> GetChallengeAsync(Guid challengeId)
	{
		var access = await statusAccessRepository.FindAsync(challengeId);
		if (access == null || access.TenantId != CurrentTenant.Id || access.EncryptedToken == null)
		{
			throw new BusinessException("AdmissionCrm:InvalidStatusOtp");
		}

		return (access, await GetLeadAsync(access));
	}

	public async Task EnsureAccessSessionAsync(
		AdmissionApplicantStatusAccess access,
		AdmissionInquiryLead lead,
		string accessSessionToken)
	{
		var now = Clock.Now;
		access.EnsureAccessSession(AdmissionCrmPublicTokenService.HashSecret(accessSessionToken), now);
		await EnsureFamilyAuthorizationAsync(access, lead, now);
	}

	public async Task EnsureFamilyAuthorizationAsync(
		AdmissionApplicantStatusAccess access,
		AdmissionInquiryLead lead,
		DateTime now)
	{
		if (!access.FamilyAccessGuardianPersonId.HasValue)
		{
			return;
		}

		var guardianPersonId = access.FamilyAccessGuardianPersonId.Value;
		var queryable = await guardianRepository.GetQueryableAsync();
		var stillAuthorized = await AsyncExecuter.AnyAsync(queryable.Where(relationship =>
			relationship.TenantId == CurrentTenant.Id &&
			relationship.LeadId == lead.Id &&
			relationship.GuardianPersonId == guardianPersonId &&
			relationship.IsActive &&
			relationship.HasPortalAccess &&
			relationship.AuthorityVerified &&
			(!relationship.EffectiveUntil.HasValue || relationship.EffectiveUntil > now)));
		if (!stillAuthorized)
		{
			throw new BusinessException("AdmissionCrm:FamilyAccessNoLongerAuthorized");
		}
	}

	public Task SaveAsync(AdmissionApplicantStatusAccess access, bool autoSave = true) =>
		statusAccessRepository.UpdateAsync(access, autoSave);

	private async Task<AdmissionInquiryLead> GetLeadAsync(AdmissionApplicantStatusAccess access)
	{
		var lead = await leadRepository.GetAsync(access.LeadId);
		if (lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		return lead;
	}
}
