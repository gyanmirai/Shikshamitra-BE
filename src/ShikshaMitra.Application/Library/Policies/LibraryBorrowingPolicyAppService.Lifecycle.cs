using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;

namespace ShikshaMitra.Library.Policies;

public partial class LibraryBorrowingPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid versionId, LibraryPolicyPublishRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var policy = await FindAsync(versionId);
		await targetAccess.ValidateAsync(policy.OriginAcademicYearId, Definition(policy));
		if (policy.Terms.DueDateMode == LibraryDueDateMode.Automatic)
			await calendar.EnsureConfiguredAsync(policy.OriginAcademicYearId);
		await publisher.PublishAsync(policy, input.ConcurrencyStamp, new DateTimeOffset(Clock.Now));
		await policies.UpdateAsync(policy, true);
		return ObjectMapper.Map<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryBorrowingPolicyVersionDto> ArchiveAsync(Guid versionId, LibraryPolicyArchiveRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var policy = await FindAsync(versionId);
		await targetAccess.ValidateAsync(policy.OriginAcademicYearId, Definition(policy));
		if (input.EffectiveOn < await calendar.TodayAsync())
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "Archiving a policy cannot change past effective dates.");
		policy.Archive(input.EffectiveOn, input.Reason, input.ConcurrencyStamp);
		await policies.UpdateAsync(policy, true);
		return ObjectMapper.Map<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>(policy);
	}

	public async Task<LibraryBorrowingPolicySnapshotDto> ResolveAsync(LibraryPolicyResolveRequest input)
	{
		await access.GetContextAsync(input.AcademicYearId);
		var date = input.AsOfDate ?? await calendar.TodayAsync();
		var context = await contextReader.ReadAsync(input, date);
		var decision = await resolver.ResolveAsync(context);
		if (decision.Policy?.Terms.DueDateMode == LibraryDueDateMode.Automatic)
			await calendar.EnsureConfiguredAsync(input.AcademicYearId);
		var result = ObjectMapper.Map<LibraryBorrowingPolicyDecision, LibraryBorrowingPolicySnapshotDto>(decision);
		var settings = await calendar.GetSettingsAsync();
		result.TimeZoneId = settings.TimeZoneId;
		result.TimeZoneSource = settings.Source;
		result.NeedsCalendarConfiguration = settings.NeedsConfiguration;
		return result;
	}

	private static LibraryBorrowingPolicyDefinition Definition(LibraryBorrowingPolicyVersion policy) => new(
		policy.Name, policy.Role, policy.EffectiveFrom, policy.EffectiveTo, policy.BookCategoryId, policy.SectionId,
		policy.EducationMediumStageId, policy.StandardId, policy.Priority, policy.Terms);
}
