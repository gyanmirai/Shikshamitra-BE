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
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionIntakeSetupAppService(
	IRepository<AdmissionIntakeSetup, Guid> setups,
	IRepository<AdmissionIntakeProfile, Guid> profiles,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionPublicInquiryLink, Guid> publicLinks,
	IAdmissionIntakeProfileAppService profileService,
	AdmissionInquiryPeopleService people)
	: ApplicationService, IAdmissionIntakeSetupAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionIntakeSetupDto>> GetListAsync(bool includeArchived = false)
	{
		var query = await setups.GetQueryableAsync();
		if (!includeArchived) query = query.Where(item => !item.IsArchived);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(item => item.Name));
		if (rows.Count == 0) return [];

		var inquiryProfileIds = rows.Select(item => item.InquiryIntakeProfileId).ToArray();
		var assignmentProfileIds = rows.Select(item => item.CounsellorAssignmentProfileId).ToArray();
		var allProfileIds = inquiryProfileIds.Concat(assignmentProfileIds).Distinct().ToArray();
		var profileRows = await profiles.GetListAsync(item => Enumerable.Contains(allProfileIds, item.Id));
		var profileMap = profileRows.ToDictionary(item => item.Id);
		var activeOfferingRows = await offerings.GetListAsync(item => item.IsActive &&
			(item.InquiryIntakeProfileId.HasValue && Enumerable.Contains(inquiryProfileIds, item.InquiryIntakeProfileId.Value) ||
			 item.CounsellorAssignmentProfileId.HasValue && Enumerable.Contains(assignmentProfileIds, item.CounsellorAssignmentProfileId.Value)));
		var now = Clock.Now.ToUniversalTime();
		var activeLinkRows = await publicLinks.GetListAsync(item => item.IsActive &&
			(!item.ExpiresAtUtc.HasValue || item.ExpiresAtUtc.Value > now) &&
			Enumerable.Contains(assignmentProfileIds, item.CounsellorAssignmentProfileId));

		return rows.Select(setup => Map(setup, profileMap,
			activeOfferingRows.Count(offering => Uses(setup, offering)),
			activeLinkRows.Count(link => link.CounsellorAssignmentProfileId == setup.CounsellorAssignmentProfileId)))
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	[UnitOfWork]
	public async Task<AdmissionIntakeSetupDto> CreateAsync(CreateAdmissionIntakeSetupDto input)
	{
		EnsureInput(input);
		await EnsurePeopleAreSelectableAsync(input.CounsellorAssignmentRules);
		var inquiry = await profileService.CreateAsync(new SaveAdmissionIntakeProfileDto
		{
			Kind = AdmissionIntakeProfileKind.Inquiry,
			Name = input.Name.Trim(),
			ConfigurationJson = Serialize(ToRules(input.InquiryRules))
		});
		var assignment = await profileService.CreateAsync(new SaveAdmissionIntakeProfileDto
		{
			Kind = AdmissionIntakeProfileKind.CounsellorAssignment,
			Name = input.Name.Trim(),
			ConfigurationJson = Serialize(ToRules(input.CounsellorAssignmentRules))
		});
		var setup = new AdmissionIntakeSetup(GuidGenerator.Create(), CurrentTenant.Id, input.Name,
			inquiry.Id, assignment.Id);
		await setups.InsertAsync(setup, autoSave: true);
		var profileRows = await profiles.GetListAsync(item => item.Id == inquiry.Id || item.Id == assignment.Id);
		return Map(setup, profileRows.ToDictionary(item => item.Id), 0, 0);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	[UnitOfWork]
	public async Task<AdmissionIntakeSetupDto> UpdateAsync(Guid id, UpdateAdmissionIntakeSetupDto input)
	{
		EnsureInput(input);
		var setup = await GetSetupAsync(id);
		EnsureConcurrency(setup, input.ConcurrencyStamp);
		if (setup.IsArchived) throw new BusinessException(AdmissionIntakeProfileErrorCodes.SetupArchived);
		await EnsurePeopleAreSelectableAsync(input.CounsellorAssignmentRules);
		var profileRows = await profiles.GetListAsync(item =>
			item.Id == setup.InquiryIntakeProfileId || item.Id == setup.CounsellorAssignmentProfileId);
		var inquiry = profileRows.SingleOrDefault(item => item.Id == setup.InquiryIntakeProfileId &&
			item.Kind == AdmissionIntakeProfileKind.Inquiry) ??
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
				"The setup inquiry profile is missing or invalid.");
		var assignment = profileRows.SingleOrDefault(item => item.Id == setup.CounsellorAssignmentProfileId &&
			item.Kind == AdmissionIntakeProfileKind.CounsellorAssignment) ??
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
				"The setup counsellor assignment profile is missing or invalid.");

		await profileService.UpdateManagedAsync(setup.Id, inquiry.Id, new SaveAdmissionIntakeProfileDto
		{
			Kind = inquiry.Kind,
			Code = inquiry.Code,
			Name = input.Name.Trim(),
			ConfigurationJson = Serialize(ToRules(input.InquiryRules)),
			ConcurrencyStamp = inquiry.ConcurrencyStamp
		});
		await profileService.UpdateManagedAsync(setup.Id, assignment.Id, new SaveAdmissionIntakeProfileDto
		{
			Kind = assignment.Kind,
			Code = assignment.Code,
			Name = input.Name.Trim(),
			ConfigurationJson = Serialize(ToRules(input.CounsellorAssignmentRules)),
			ConcurrencyStamp = assignment.ConcurrencyStamp
		});
		setup.UpdateName(input.Name);
		await setups.UpdateAsync(setup, autoSave: true);
		var usage = await GetUsageCoreAsync(setup);
		var updatedProfiles = await profiles.GetListAsync(item =>
			item.Id == setup.InquiryIntakeProfileId || item.Id == setup.CounsellorAssignmentProfileId);
		return Map(setup, updatedProfiles.ToDictionary(item => item.Id),
			usage.ActiveOfferingCount, usage.ActivePublicInquiryLinkCount);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<AdmissionIntakeSetupUsageDto> GetUsageAsync(Guid id) =>
		await GetUsageCoreAsync(await GetSetupAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	[UnitOfWork]
	public async Task<AdmissionIntakeSetupDto> ArchiveAsync(Guid id, ArchiveAdmissionIntakeSetupDto input)
	{
		var setup = await GetSetupAsync(id);
		EnsureConcurrency(setup, input?.ConcurrencyStamp);
		if (!setup.IsArchived)
		{
			var usage = await GetUsageCoreAsync(setup);
			if (usage.HasActiveUsage)
				throw new BusinessException(AdmissionIntakeProfileErrorCodes.SetupInUse,
					$"This setup is used by {usage.ActiveOfferingCount} active offering(s) and " +
					$"{usage.ActivePublicInquiryLinkCount} active public inquiry link(s).");
			setup.Archive(Clock.Now, CurrentUser.Id);
			await setups.UpdateAsync(setup, autoSave: true);
		}
		var profileRows = await profiles.GetListAsync(item =>
			item.Id == setup.InquiryIntakeProfileId || item.Id == setup.CounsellorAssignmentProfileId);
		return Map(setup, profileRows.ToDictionary(item => item.Id), 0, 0);
	}

	private async Task<AdmissionIntakeSetupUsageDto> GetUsageCoreAsync(AdmissionIntakeSetup setup)
	{
		var now = Clock.Now.ToUniversalTime();
		var offeringRows = await offerings.GetListAsync(item =>
			(item.InquiryIntakeProfileId == setup.InquiryIntakeProfileId ||
			 item.CounsellorAssignmentProfileId == setup.CounsellorAssignmentProfileId));
		var linkRows = await publicLinks.GetListAsync(item =>
			item.CounsellorAssignmentProfileId == setup.CounsellorAssignmentProfileId);
		return new AdmissionIntakeSetupUsageDto
		{
			SetupId = setup.Id,
			Offerings = offeringRows.OrderByDescending(item => item.IsActive).ThenBy(item => item.Name).Select(item =>
				new AdmissionIntakeSetupOfferingUsageDto
				{
					Id = item.Id, Name = item.Name, Code = item.Code,
					AdmissionCycleId = item.AdmissionCycleId, EntryMode = item.EntryMode,
					IsActive = item.IsActive
				}).ToList(),
			PublicInquiryLinks = linkRows.OrderByDescending(item => item.IsActive).ThenBy(item => item.Name).Select(item =>
				new AdmissionIntakeSetupPublicLinkUsageDto
				{
					Id = item.Id, Name = item.Name, AdmissionCycleId = item.AdmissionCycleId,
					ExpiresAtUtc = item.ExpiresAtUtc, IsActive = item.IsActive,
					IsOperational = item.IsActive && (!item.ExpiresAtUtc.HasValue || item.ExpiresAtUtc.Value > now)
				}).ToList()
		};
	}

	private async Task<AdmissionIntakeSetup> GetSetupAsync(Guid id) =>
		await setups.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionIntakeSetup), id);

	private async Task EnsurePeopleAreSelectableAsync(AdmissionCounsellorAssignmentRulesDto rules)
	{
		var ids = rules.CounsellorUserIds.Append(rules.FallbackManagerUserId);
		await people.EnsureSelectableAsync(ids);
	}

	private static void EnsureInput(CreateAdmissionIntakeSetupDto input)
	{
		if (input == null || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 128 ||
			input.InquiryRules == null || input.CounsellorAssignmentRules == null ||
			input.CounsellorAssignmentRules.FallbackManagerUserId == Guid.Empty)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		try
		{
			ToRules(input.InquiryRules).Validate();
			ToRules(input.CounsellorAssignmentRules).Validate();
		}
		catch (ArgumentException error)
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration, error.Message);
		}
	}

	private static void EnsureConcurrency(AdmissionIntakeSetup setup, string? concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp) || setup.ConcurrencyStamp != concurrencyStamp)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.ConcurrencyConflict);
	}

	private static AdmissionInquiryIntakeRules ToRules(AdmissionInquiryIntakeRulesDto input) => new AdmissionInquiryIntakeRules
	{
		DuplicateRule = input.DuplicateRule,
		ApplicationStartRequirement = input.ApplicationStartRequirement,
		FirstContactDueInHours = input.FirstContactDueInHours,
		QualificationDueInDays = input.QualificationDueInDays,
		FollowUpDueInDays = input.FollowUpDueInDays,
		MaxOpenFollowUps = input.MaxOpenFollowUps,
		RequireGuardianEmail = input.RequireGuardianEmail,
		RequireApplicantDateOfBirth = input.RequireApplicantDateOfBirth,
		AllowedSources = input.AllowedSources,
		RequireSourceDetailFor = input.RequireSourceDetailFor
	}.WithStandardBehavior();

	private static AdmissionCounsellorAssignmentRules ToRules(AdmissionCounsellorAssignmentRulesDto input) => new()
	{
		CounsellorUserIds = input.CounsellorUserIds,
		MaxActiveCasesPerCounsellor = input.MaxActiveCasesPerCounsellor,
		FallbackManagerUserId = input.FallbackManagerUserId
	};

	private static AdmissionIntakeSetupDto Map(AdmissionIntakeSetup setup,
		IReadOnlyDictionary<Guid, AdmissionIntakeProfile> profileMap, int activeOfferingCount,
		int activePublicInquiryLinkCount)
	{
		if (!profileMap.TryGetValue(setup.InquiryIntakeProfileId, out var inquiry) ||
			!profileMap.TryGetValue(setup.CounsellorAssignmentProfileId, out var assignment))
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration,
				"One or more profiles belonging to this setup are missing.");
		var isConfigurationValid = true;
		string? configurationIssue = null;
		AdmissionInquiryIntakeRules inquiryRules;
		AdmissionCounsellorAssignmentRules assignmentRules;
		try
		{
			inquiryRules = JsonSerializer.Deserialize<AdmissionInquiryIntakeRules>(inquiry.ConfigurationJson, JsonOptions) ??
				throw new InvalidOperationException("Inquiry rules are missing.");
			assignmentRules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(assignment.ConfigurationJson, JsonOptions) ??
				throw new InvalidOperationException("Counsellor assignment rules are missing.");
			inquiryRules = inquiryRules.WithStandardBehavior();
			inquiryRules.Validate();
			assignmentRules.Validate();
			if (!assignmentRules.FallbackManagerUserId.HasValue)
				throw new InvalidOperationException("A fallback manager is required for this setup.");
		}
		catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
		{
			isConfigurationValid = false;
			configurationIssue = error.Message;
			inquiryRules = new AdmissionInquiryIntakeRules();
			assignmentRules = new AdmissionCounsellorAssignmentRules();
		}
		return new AdmissionIntakeSetupDto
		{
			Id = setup.Id,
			Name = setup.Name,
			InquiryIntakeProfileId = setup.InquiryIntakeProfileId,
			CounsellorAssignmentProfileId = setup.CounsellorAssignmentProfileId,
			InquiryRules = new AdmissionInquiryIntakeRulesDto
			{
				DuplicateRule = inquiryRules.DuplicateRule,
				ApplicationStartRequirement = inquiryRules.ApplicationStartRequirement,
				FirstContactDueInHours = inquiryRules.FirstContactDueInHours,
				QualificationDueInDays = inquiryRules.QualificationDueInDays,
				FollowUpDueInDays = inquiryRules.FollowUpDueInDays,
				MaxOpenFollowUps = inquiryRules.MaxOpenFollowUps,
				RequireGuardianEmail = inquiryRules.RequireGuardianEmail,
				RequireApplicantDateOfBirth = inquiryRules.RequireApplicantDateOfBirth,
				AllowedSources = inquiryRules.AllowedSources,
				RequireSourceDetailFor = inquiryRules.RequireSourceDetailFor
			},
			CounsellorAssignmentRules = new AdmissionCounsellorAssignmentRulesDto
			{
				CounsellorUserIds = assignmentRules.CounsellorUserIds,
				MaxActiveCasesPerCounsellor = assignmentRules.MaxActiveCasesPerCounsellor,
				FallbackManagerUserId = assignmentRules.FallbackManagerUserId ?? Guid.Empty
			},
			IsArchived = setup.IsArchived,
			ArchivedTime = setup.ArchivedTime,
			ActiveOfferingCount = activeOfferingCount,
			ActivePublicInquiryLinkCount = activePublicInquiryLinkCount,
			IsConfigurationValid = isConfigurationValid,
			ConfigurationIssue = configurationIssue,
			ConcurrencyStamp = setup.ConcurrencyStamp
		};
	}

	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

	private static bool Uses(AdmissionIntakeSetup setup, AdmissionOffering offering) =>
		offering.InquiryIntakeProfileId == setup.InquiryIntakeProfileId ||
		offering.CounsellorAssignmentProfileId == setup.CounsellorAssignmentProfileId;
}
