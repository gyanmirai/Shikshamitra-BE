using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmPublicFamilyLoginEntry(
	AdmissionApplicantStatusAccess Access,
	AdmissionInquiryLead Lead,
	Guid? GuardianPersonId,
	string? DeliveryContact);

public class AdmissionCrmPublicFamilyDirectory(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionApplicantStatusAccess, Guid> accessRepository,
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
	AdmissionCrmPublicStatusCoreReader coreReader,
	AdmissionCrmPublicTokenService tokenService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPublicFamilyLoginEntry?> FindLoginEntryAsync(string contact, DateTime now)
	{
		var trimmed = contact.Trim();
		var isEmail = trimmed.Contains('@');
		if (isEmail && !AdmissionCrmContactRules.IsValidEmail(trimmed) ||
			!isEmail && !AdmissionCrmContactRules.IsValidPhone(trimmed))
		{
			return null;
		}

		var guardianEntry = await FindGuardianEntryAsync(trimmed, isEmail, now);
		return guardianEntry ?? await FindLeadContactEntryAsync(trimmed, isEmail, now);
	}

	public async Task<List<AdmissionFamilyApplicationSummaryDto>> ShareSessionAsync(
		AdmissionInquiryLead verifiedLead,
		AdmissionApplicantStatusAccess verifiedAccess,
		string sessionTokenHash,
		DateTime sessionExpiresAt,
		DateTime now)
	{
		var entries = await GetEntriesAsync(verifiedLead, verifiedAccess, now);
		foreach (var (_, access) in entries.Where(entry => entry.Access.Id != verifiedAccess.Id))
		{
			access.ShareVerifiedFamilyAccessSession(verifiedAccess, sessionTokenHash, sessionExpiresAt, now);
			await accessRepository.UpdateAsync(access, autoSave: false);
		}
		return await ToSummariesAsync(entries);
	}

	public async Task<List<AdmissionFamilyApplicationSummaryDto>> GetSummariesAsync(
		AdmissionInquiryLead verifiedLead,
		AdmissionApplicantStatusAccess verifiedAccess,
		DateTime now) => await ToSummariesAsync(await GetEntriesAsync(verifiedLead, verifiedAccess, now));

	internal static string MaskSubmittedContact(string contact)
	{
		var trimmed = contact.Trim();
		if (trimmed.Contains('@'))
		{
			var separator = trimmed.IndexOf('@');
			return separator <= 0 ? "provided contact" : $"{trimmed[0]}***{trimmed[separator..]}";
		}
		var digits = new string(trimmed.Where(character => character is >= '0' and <= '9').ToArray());
		return digits.Length >= 4 ? $"***{digits[^4..]}" : "provided contact";
	}

	private async Task<AdmissionCrmPublicFamilyLoginEntry?> FindGuardianEntryAsync(
		string contact, bool isEmail, DateTime now)
	{
		var normalizedEmail = isEmail ? contact.ToUpperInvariant() : null;
		var normalizedPhone = isEmail ? null : new string(contact.Where(char.IsDigit).ToArray());
		var people = await AsyncExecuter.ToListAsync((await personRepository.GetQueryableAsync()).Where(person =>
			person.TenantId == CurrentTenant.Id &&
			(isEmail ? person.NormalizedEmail == normalizedEmail : person.NormalizedPhone == normalizedPhone)));
		if (people.Count == 0) return null;

		var personIds = people.Select(person => person.Id).ToList();
		var relationships = await AsyncExecuter.ToListAsync((await guardianRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && personIds.Contains(item.GuardianPersonId) && item.IsActive &&
			item.HasPortalAccess && item.AuthorityVerified && (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now)));
		if (relationships.Count == 0) return null;

		var leadIds = relationships.Select(item => item.LeadId).Distinct().ToList();
		var access = (await AsyncExecuter.ToListAsync((await accessRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && leadIds.Contains(item.LeadId) && item.TokenExpiresAt > now &&
			item.EncryptedToken != null))).OrderByDescending(item => item.LinkIssuedAt).FirstOrDefault();
		if (access == null) return null;
		return new(access, await leadRepository.GetAsync(access.LeadId),
			relationships.First(item => item.LeadId == access.LeadId).GuardianPersonId, contact);
	}

	private async Task<AdmissionCrmPublicFamilyLoginEntry?> FindLeadContactEntryAsync(
		string contact, bool isEmail, DateTime now)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var normalizedPhone = new string(contact.Where(character => character >= '0' && character <= '9').ToArray());
		var leads = isEmail
			? await AsyncExecuter.ToListAsync(queryable.Where(lead =>
				lead.TenantId == CurrentTenant.Id && lead.NormalizedEmail == contact.ToLowerInvariant()))
			: await AsyncExecuter.ToListAsync(queryable.Where(lead =>
				lead.TenantId == CurrentTenant.Id && lead.NormalizedPrimaryContactNo == normalizedPhone));
		if (leads.Count == 0) return null;
		var leadIds = leads.Select(lead => lead.Id).ToList();
		var access = (await AsyncExecuter.ToListAsync((await accessRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && leadIds.Contains(item.LeadId) && item.TokenExpiresAt > now &&
			item.EncryptedToken != null))).OrderByDescending(item => item.LinkIssuedAt).FirstOrDefault();
		return access == null ? null : new(access, leads.Single(lead => lead.Id == access.LeadId), null, null);
	}

	private async Task<List<(AdmissionInquiryLead Lead, AdmissionApplicantStatusAccess Access)>> GetEntriesAsync(
		AdmissionInquiryLead verifiedLead, AdmissionApplicantStatusAccess verifiedAccess, DateTime now)
	{
		var leadQueryable = await leadRepository.GetQueryableAsync();
		List<AdmissionInquiryLead> leads;
		if (verifiedAccess.FamilyAccessGuardianPersonId.HasValue)
		{
			var guardianId = verifiedAccess.FamilyAccessGuardianPersonId.Value;
			var relationships = await AsyncExecuter.ToListAsync((await guardianRepository.GetQueryableAsync()).Where(item =>
				item.TenantId == CurrentTenant.Id && item.GuardianPersonId == guardianId && item.IsActive &&
				item.HasPortalAccess && item.AuthorityVerified && (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now)));
			var authorizedLeadIds = relationships.Select(item => item.LeadId).Distinct().ToList();
			leads = await AsyncExecuter.ToListAsync(leadQueryable.Where(lead =>
				lead.TenantId == CurrentTenant.Id && authorizedLeadIds.Contains(lead.Id)));
		}
		else if (!string.IsNullOrWhiteSpace(verifiedLead.NormalizedPrimaryContactNo))
		{
			leads = await AsyncExecuter.ToListAsync(leadQueryable.Where(lead => lead.TenantId == CurrentTenant.Id &&
				lead.NormalizedPrimaryContactNo == verifiedLead.NormalizedPrimaryContactNo));
		}
		else
		{
			leads = await AsyncExecuter.ToListAsync(leadQueryable.Where(lead =>
				lead.TenantId == CurrentTenant.Id && lead.NormalizedEmail == verifiedLead.NormalizedEmail));
		}

		var ids = leads.Select(lead => lead.Id).ToList();
		var accesses = await AsyncExecuter.ToListAsync((await accessRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && ids.Contains(item.LeadId) && item.TokenExpiresAt > now &&
			item.EncryptedToken != null));
		var latest = accesses.GroupBy(item => item.LeadId)
			.ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LinkIssuedAt).First());
		return leads.Where(lead => latest.ContainsKey(lead.Id)).Select(lead => (lead, latest[lead.Id]))
			.OrderByDescending(entry => entry.lead.LastActivityTime).ToList();
	}

	private async Task<List<AdmissionFamilyApplicationSummaryDto>> ToSummariesAsync(
		IReadOnlyList<(AdmissionInquiryLead Lead, AdmissionApplicantStatusAccess Access)> entries)
	{
		var summaries = new List<AdmissionFamilyApplicationSummaryDto>(entries.Count);
		foreach (var (lead, access) in entries)
		{
			var statusToken = tokenService.DecryptRequired(access.EncryptedToken!);
			summaries.Add(new AdmissionFamilyApplicationSummaryDto
			{
				LeadId = lead.Id, AcademicYearId = lead.AcademicYearId, StatusToken = statusToken,
				DisplayName = AdmissionCrmPublicProgressMapper.BuildDisplayName(lead),
				DesiredClassName = lead.DesiredClassName, Stage = lead.Stage,
				StageName = await coreReader.GetStageNameAsync(lead.AcademicYearId, lead.Stage),
				LastActivityTime = lead.LastActivityTime
			});
		}
		return summaries;
	}
}
