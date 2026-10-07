using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionIntakeAssignmentService(
	IAdmissionIntakeProfileRepository profiles,
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<StudentOnboardingCase, Guid> onboardingCases,
	IRepository<IdentityUser, Guid> users,
	AdmissionInquiryPeopleService people) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCounsellorAssignmentRules> GetRulesAsync(Guid offeringId)
	{
		var offering = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetAsync(offeringId);
		if (!offering.CounsellorAssignmentProfileId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired, "Configure an intake counsellor pool before transferring this inquiry.");
		var profile = await profiles.GetAsync(offering.CounsellorAssignmentProfileId.Value);
		if (profile.Kind != AdmissionIntakeProfileKind.CounsellorAssignment)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		try
		{
			var rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(profile.ConfigurationJson,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Assignment settings are missing.");
			rules.Validate();
			return rules;
		}
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration, exception.Message);
		}
	}

	public async Task EnsureManualTransferAsync(Guid offeringId, Guid counsellorId, Guid inquiryId)
	{
		var rules = await GetRulesAsync(offeringId);
		if (!rules.CounsellorUserIds.Contains(counsellorId) && rules.FallbackManagerUserId != counsellorId)
			throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid, "Choose a counsellor from the offering's configured pool.");
		await people.EnsureCanAccessOfferingAsync(counsellorId, offeringId);
		var offering = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetAsync(offeringId);
		await profiles.AcquireAssignmentLockAsync(offering.CounsellorAssignmentProfileId!.Value);
		var load = await inquiries.CountAsync(item => item.Id != inquiryId && item.AssignedCounsellorId == counsellorId &&
			item.Status != AdmissionInquiryStatus.ApplicationStarted && item.Status != AdmissionInquiryStatus.Converted &&
			item.Status != AdmissionInquiryStatus.NotInterested && item.Status != AdmissionInquiryStatus.Lost && item.Status != AdmissionInquiryStatus.Duplicate);
		load += await applications.CountAsync(item => item.AssignedCounsellorId == counsellorId && item.Status != AdmissionApplicationStatus.Withdrawn &&
			item.Status != AdmissionApplicationStatus.Rejected && item.Status != AdmissionApplicationStatus.OfferDeclined && item.Status != AdmissionApplicationStatus.Enrolled && item.Status != AdmissionApplicationStatus.AdmissionFinalized);
		load += await onboardingCases.CountAsync(item => item.AssignedToUserId == counsellorId && item.Status != StudentOnboardingStatus.Completed && item.Status != StudentOnboardingStatus.Cancelled);
		if (load >= rules.MaxActiveCasesPerCounsellor)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired, "The selected counsellor is at capacity.");
	}

	public async Task<Guid?> ResolveAsync(AdmissionOffering offering)
	{
		if (!offering.CounsellorAssignmentProfileId.HasValue) return null;
		return await ResolveAsync(offering.CounsellorAssignmentProfileId.Value, offering.Id, null);
	}

	public Task<Guid?> ResolveForCycleAsync(Guid assignmentProfileId, Guid cycleId) =>
		ResolveAsync(assignmentProfileId, null, cycleId);

	private async Task<Guid?> ResolveAsync(Guid assignmentProfileId, Guid? offeringId, Guid? cycleId)
	{
		var profile = await profiles.FindAsync(assignmentProfileId);
		if (profile?.Kind != AdmissionIntakeProfileKind.CounsellorAssignment)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		await profiles.AcquireAssignmentLockAsync(profile.Id);
		AdmissionCounsellorAssignmentRules rules;
		try
		{
			rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(profile.ConfigurationJson,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
				throw new ArgumentException("Assignment settings are missing.");
			rules.Validate();
		}
		catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException)
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration, error.Message);
		}

		var configuredActiveUsers = await AsyncExecuter.ToListAsync((await users.GetQueryableAsync())
			.Where(user => rules.CounsellorUserIds.Contains(user.Id) && user.IsActive)
			.Select(user => user.Id));
		var activeUsers = new System.Collections.Generic.List<Guid>();
		foreach (var id in configuredActiveUsers)
			if (offeringId.HasValue
				? await people.CanAccessOfferingAsync(id, offeringId.Value)
				: await people.CanAccessCycleAsync(id, cycleId!.Value))
				activeUsers.Add(id);
		var capacity = rules.MaxActiveCasesPerCounsellor;
		var inquiryCounts = await AsyncExecuter.ToListAsync((await inquiries.GetQueryableAsync())
			.Where(item => item.AssignedCounsellorId.HasValue && activeUsers.Contains(item.AssignedCounsellorId.Value) &&
				item.Status != AdmissionInquiryStatus.ApplicationStarted &&
				item.Status != AdmissionInquiryStatus.Converted &&
				item.Status != AdmissionInquiryStatus.NotInterested &&
				item.Status != AdmissionInquiryStatus.Lost &&
				item.Status != AdmissionInquiryStatus.Duplicate)
			.GroupBy(item => item.AssignedCounsellorId!.Value)
			.Select(group => new { Id = group.Key, Count = group.Count() }));
		var applicationCounts = await AsyncExecuter.ToListAsync((await applications.GetQueryableAsync())
			.Where(item => item.AssignedCounsellorId.HasValue && activeUsers.Contains(item.AssignedCounsellorId.Value) &&
				item.Status != AdmissionApplicationStatus.Withdrawn &&
				item.Status != AdmissionApplicationStatus.Rejected &&
				item.Status != AdmissionApplicationStatus.OfferDeclined &&
				item.Status != AdmissionApplicationStatus.Enrolled &&
				item.Status != AdmissionApplicationStatus.AdmissionFinalized)
			.GroupBy(item => item.AssignedCounsellorId!.Value)
			.Select(group => new { Id = group.Key, Count = group.Count() }));
		var onboardingCounts = await AsyncExecuter.ToListAsync((await onboardingCases.GetQueryableAsync())
			.Where(item => item.AssignedToUserId.HasValue && activeUsers.Contains(item.AssignedToUserId.Value) &&
				item.Status != StudentOnboardingStatus.Completed && item.Status != StudentOnboardingStatus.Cancelled)
			.GroupBy(item => item.AssignedToUserId!.Value)
			.Select(group => new { Id = group.Key, Count = group.Count() }));
		var load = activeUsers.Select(id => (Id: id,
			Count: (inquiryCounts.FirstOrDefault(item => item.Id == id)?.Count ?? 0) +
				(applicationCounts.FirstOrDefault(item => item.Id == id)?.Count ?? 0) +
				(onboardingCounts.FirstOrDefault(item => item.Id == id)?.Count ?? 0)));
		var selected = load.Where(item => item.Count < capacity)
			.OrderBy(item => item.Count).ThenBy(item => item.Id).FirstOrDefault();
		if (selected.Id != Guid.Empty) return selected.Id;
		if (rules.FallbackManagerUserId.HasValue)
		{
			var fallback = await users.FindAsync(rules.FallbackManagerUserId.Value);
			if (fallback?.IsActive != true)
				throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid,
					"The fallback manager is unavailable.");
			var eligible = offeringId.HasValue
				? await people.CanAccessOfferingAsync(fallback.Id, offeringId.Value)
				: await people.CanAccessCycleAsync(fallback.Id, cycleId!.Value);
			if (!eligible)
				throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid,
					"The fallback manager cannot access this admission intake.");
			return fallback.Id;
		}
		throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired,
			"All counsellors are at capacity. Set a fallback manager or increase capacity.");
	}
}
