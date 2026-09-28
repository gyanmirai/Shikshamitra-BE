using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionIntakeProfileAppService(IRepository<AdmissionIntakeProfile, Guid> profiles,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionPublicInquiryLink, Guid> publicLinks,
	AdmissionInquiryPeopleService people)
	: ApplicationService, IAdmissionIntakeProfileAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionIntakeProfileDto>> GetListAsync(AdmissionIntakeProfileKind? kind = null)
	{
		if (kind.HasValue && !Enum.IsDefined(kind.Value))
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		var query = await profiles.GetQueryableAsync();
		if (kind.HasValue) query = query.Where(item => item.Kind == kind.Value);
		return (await AsyncExecuter.ToListAsync(query.OrderBy(item => item.Kind).ThenBy(item => item.Name)))
			.Select(Map).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter = null) =>
		people.GetCounsellorsAsync(filter);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionIntakeProfileDto> CreateAsync(SaveAdmissionIntakeProfileDto input)
	{
		EnsureIdentity(input);
		Validate(input.Kind, input.ConfigurationJson);
		await EnsureUniqueAsync(input.Kind, input.Code, null);
		var profile = new AdmissionIntakeProfile(GuidGenerator.Create(), CurrentTenant.Id,
			input.Kind, input.Code, input.Name, input.ConfigurationJson);
		try { await profiles.InsertAsync(profile, true); }
		catch (Exception error) when (IsUniqueConstraintViolation(error))
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.DuplicateCode);
		}
		return Map(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionIntakeProfileDto> UpdateAsync(Guid id, SaveAdmissionIntakeProfileDto input)
	{
		EnsureIdentity(input);
		var profile = await profiles.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionIntakeProfile), id);
		if (profile.Kind != input.Kind)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
				"The profile type cannot be changed.");
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || profile.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.ConcurrencyConflict);
		Validate(input.Kind, input.ConfigurationJson);
		await EnsureActiveOfferingsCanUseAsync(id, input.Kind, input.ConfigurationJson);
		await EnsureUniqueAsync(input.Kind, input.Code, id);
		profile.Update(input.Code, input.Name, input.ConfigurationJson);
		try { await profiles.UpdateAsync(profile, true); }
		catch (Exception error) when (IsUniqueConstraintViolation(error))
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.DuplicateCode);
		}
		return Map(profile);
	}

	private static void EnsureIdentity(SaveAdmissionIntakeProfileDto input)
	{
		if (input == null || !Enum.IsDefined(input.Kind) || string.IsNullOrWhiteSpace(input.Code) ||
			string.IsNullOrWhiteSpace(input.Name) || input.Code.Length > 64 || input.Name.Length > 128 ||
			string.IsNullOrWhiteSpace(input.ConfigurationJson) || input.ConfigurationJson.Length > 8192)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
	}

	private static bool IsUniqueConstraintViolation(Exception error)
	{
		for (var current = error; current != null; current = current.InnerException)
			if (current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString() == "23505")
				return true;
		return false;
	}

	private async Task EnsureUniqueAsync(AdmissionIntakeProfileKind kind, string code, Guid? excludingId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var query = await profiles.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(item => item.Kind == kind && item.Code == normalized &&
			(!excludingId.HasValue || item.Id != excludingId.Value))))
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.DuplicateCode);
	}

	private static void Validate(AdmissionIntakeProfileKind kind, string json)
	{
		try
		{
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			if (kind == AdmissionIntakeProfileKind.Inquiry)
				(JsonSerializer.Deserialize<AdmissionInquiryIntakeRules>(json, options) ??
					throw new ArgumentException("Inquiry settings are required.")).Validate();
			else if (kind == AdmissionIntakeProfileKind.CounsellorAssignment)
				(JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(json, options) ??
					throw new ArgumentException("Counsellor assignment settings are required.")).Validate();
			else throw new ArgumentException("Choose a supported profile type.");
		}
		catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException)
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration, error.Message);
		}
	}

	private async Task EnsureActiveOfferingsCanUseAsync(Guid profileId, AdmissionIntakeProfileKind kind, string json)
	{
		if (kind != AdmissionIntakeProfileKind.CounsellorAssignment) return;
		var rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
		foreach (var offering in await offerings.GetListAsync(item =>
			item.CounsellorAssignmentProfileId == profileId && item.IsActive))
		{
			var eligible = false;
			foreach (var userId in rules.CounsellorUserIds)
				if (await people.CanAccessOfferingAsync(userId, offering.Id)) { eligible = true; break; }
			if (!eligible || rules.FallbackManagerUserId.HasValue &&
				!await people.CanAccessOfferingAsync(rules.FallbackManagerUserId.Value, offering.Id))
				throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
					$"This assignment would leave active offering {offering.Code} without a scoped counsellor or fallback manager.");
		}
		foreach (var link in await publicLinks.GetListAsync(item =>
			item.CounsellorAssignmentProfileId == profileId && item.IsActive))
		{
			var eligible = false;
			foreach (var userId in rules.CounsellorUserIds)
				if (await people.CanAccessCycleAsync(userId, link.AdmissionCycleId)) { eligible = true; break; }
			if (!eligible || rules.FallbackManagerUserId.HasValue &&
				!await people.CanAccessCycleAsync(rules.FallbackManagerUserId.Value, link.AdmissionCycleId))
				throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
					$"This assignment would leave active inquiry link {link.Name} without a scoped counsellor or fallback manager.");
		}
	}

	private static AdmissionIntakeProfileDto Map(AdmissionIntakeProfile profile) => new()
	{
		Id = profile.Id,
		Kind = profile.Kind,
		Code = profile.Code,
		Name = profile.Name,
		ConfigurationJson = profile.ConfigurationJson,
		ConcurrencyStamp = profile.ConcurrencyStamp
	};
}
