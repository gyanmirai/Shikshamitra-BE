using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantStatusLinkWorkflow(
	IRepository<AdmissionApplicantStatusAccess, Guid> repository,
	IStringEncryptionService encryption) : ApplicationService, ITransientDependency
{
	public async Task<string> EnsureAsync(AdmissionInquiryLead lead, DateTime expiresAt)
	{
		var access = await FindAsync(lead.Id);
		string rawToken;
		if (access == null)
		{
			rawToken = CreateRawToken();
			access = new AdmissionApplicantStatusAccess(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, Hash(rawToken), Encrypt(rawToken), expiresAt, Clock.Now);
			await repository.InsertAsync(access, autoSave: false);
		}
		else
		{
			rawToken = string.IsNullOrWhiteSpace(access.EncryptedToken) ? string.Empty : encryption.Decrypt(access.EncryptedToken) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(rawToken) || access.TokenExpiresAt <= Clock.Now)
			{
				rawToken = CreateRawToken();
				access.ReissueLink(Hash(rawToken), Encrypt(rawToken), expiresAt, Clock.Now);
				await repository.UpdateAsync(access, autoSave: false);
			}
		}
		return Path(rawToken);
	}

	public async Task<string?> GetExistingAsync(Guid leadId)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks))
			return null;
		var access = await FindAsync(leadId);
		if (access == null || access.TokenExpiresAt <= Clock.Now || string.IsNullOrWhiteSpace(access.EncryptedToken)) return null;
		var rawToken = encryption.Decrypt(access.EncryptedToken);
		return string.IsNullOrWhiteSpace(rawToken) ? null : Path(rawToken);
	}

	public async Task<string> ReissueAsync(AdmissionInquiryLead lead, DateTime expiresAt)
	{
		var access = await FindAsync(lead.Id);
		var rawToken = CreateRawToken();
		if (access == null)
		{
			access = new AdmissionApplicantStatusAccess(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, Hash(rawToken), Encrypt(rawToken), expiresAt, Clock.Now);
			await repository.InsertAsync(access, autoSave: false);
		}
		else
		{
			access.ReissueLink(Hash(rawToken), Encrypt(rawToken), expiresAt, Clock.Now);
			await repository.UpdateAsync(access, autoSave: false);
		}
		return Path(rawToken);
	}

	private async Task<AdmissionApplicantStatusAccess?> FindAsync(Guid leadId)
	{
		var queryable = await repository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private string Encrypt(string value) => encryption.Encrypt(value) ?? throw new BusinessException("AdmissionCrm:EncryptionFailed");
	private string Path(string rawToken) => $"/student/admissions/status/{CurrentTenant.Id:D}/{rawToken}";
	private static string CreateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
}
