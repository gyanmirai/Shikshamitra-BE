using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyVersionReadService(
	IRepository<HrPolicyVersion, Guid> versionRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<IdentityUser, Guid> userRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(
		string policyType,
		Guid policyId,
		PagedAndSortedResultRequestDto input, string? search = null, string? action = null)
	{
		var normalized = HrPolicyVersionProjection.NormalizePolicyType(policyType);
		var query = (await versionRepository.GetQueryableAsync())
			.Where(x => x.PolicyType == normalized && x.PolicyId == policyId);
		if (!string.IsNullOrWhiteSpace(action) && action != "all") query = query.Where(x => x.ChangeAction == action);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
			var userIds = (await userRepository.GetQueryableAsync()).Where(x =>
				(x.Name ?? "").ToLower().Contains(term) || (x.Surname ?? "").ToLower().Contains(term) || x.UserName.ToLower().Contains(term)).Select(x => x.Id);
			var versionNumber = int.TryParse(term.TrimStart('v'), out var number) ? number : -1;
			query = query.Where(x => x.Name.ToLower().Contains(term) || (x.ChangeReason ?? "").ToLower().Contains(term)
				|| x.ChangeAction.ToLower().Replace("_", " ").Replace("-", " ").Contains(term) || x.VersionNumber == versionNumber
				|| (x.CreatorId.HasValue && userIds.Contains(x.CreatorId.Value)));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));
		var dtos = rows.Select(HrPolicyVersionProjection.ToDto).ToList();
		await HydrateVersionCreatorsAsync(dtos);
		return new PagedResultDto<HrPolicyVersionDto>(total, dtos);
	}

	public async Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId)
	{
		var entity = await GetVersionEntityAsync(policyType, policyId, versionId);
		var dto = HrPolicyVersionProjection.ToDto(entity);
		await HydrateVersionCreatorsAsync([dto]);
		return dto;
	}

	public async Task<HrPolicyVersionCompareDto> CompareAsync(
		string policyType,
		Guid policyId,
		Guid fromVersionId,
		Guid toVersionId)
	{
		var from = await GetVersionEntityAsync(policyType, policyId, fromVersionId);
		var to = await GetVersionEntityAsync(policyType, policyId, toVersionId);
		var displayContext = await BuildDisplayContextAsync(from.SnapshotJson, to.SnapshotJson);
		var fromDto = HrPolicyVersionProjection.ToDto(from);
		var toDto = HrPolicyVersionProjection.ToDto(to);
		await HydrateVersionCreatorsAsync([fromDto, toDto]);

		return new HrPolicyVersionCompareDto
		{
			PolicyType = HrPolicyVersionProjection.NormalizePolicyType(policyType),
			PolicyId = policyId,
			FromVersion = fromDto,
			ToVersion = toDto,
			Differences = HrPolicyVersionDiffBuilder.BuildDifferences(from.SnapshotJson, to.SnapshotJson, displayContext)
		};
	}

	public async Task<HrPolicyVersion> GetVersionEntityAsync(string policyType, Guid policyId, Guid versionId)
	{
		var normalized = HrPolicyVersionProjection.NormalizePolicyType(policyType);
		var entity = await versionRepository.GetAsync(versionId);
		if (entity.PolicyType != normalized || entity.PolicyId != policyId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyVersionNotFound);
		}

		return entity;
	}

	private async Task HydrateVersionCreatorsAsync(List<HrPolicyVersionDto> versions)
	{
		var creatorIds = versions
			.Select(x => x.CreatorId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		if (creatorIds.Count == 0)
		{
			return;
		}

		var users = await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => creatorIds.Contains(x.Id)));
		var namesById = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
		foreach (var version in versions)
		{
			if (version.CreatorId.HasValue)
			{
				version.CreatedByName = namesById.GetValueOrDefault(version.CreatorId.Value) ?? "Unknown user";
			}
		}
	}

	public async Task<List<HrPolicyVersionDifferenceDto>> CompareProposedRulesAsync(object current, object proposed)
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var currentProperties = JsonSerializer.SerializeToElement(current, options).EnumerateObject()
			.ToDictionary(x => x.Name, x => x.Value.Clone());
		var proposedProperties = JsonSerializer.SerializeToElement(proposed, options).EnumerateObject()
			.Where(x => currentProperties.ContainsKey(x.Name)).ToDictionary(x => x.Name, x => x.Value.Clone());
		HrPolicyProposalCollectionComparison.Normalize(currentProperties);
		HrPolicyProposalCollectionComparison.Normalize(proposedProperties);
		// Compare the update contract only. IDs, versions, audit timestamps and other
		// response-only metadata are not removed rules when a form omits them.
		var fromJson = JsonSerializer.Serialize(currentProperties.Where(x => proposedProperties.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value), options);
		var toJson = JsonSerializer.Serialize(proposedProperties, options);
		var differences = HrPolicyVersionDiffBuilder.BuildDifferences(fromJson, toJson, await BuildDisplayContextAsync(fromJson, toJson));
		foreach (var difference in differences.Where(x => HrPolicyProposalCollectionComparison.Supports(x.Field)))
		{
			difference.FromDisplayValue = difference.FromValue = HrPolicyProposalCollectionComparison.Display(difference.Field, currentProperties[difference.Field]);
			difference.ToDisplayValue = difference.ToValue = HrPolicyProposalCollectionComparison.Display(difference.Field, proposedProperties[difference.Field]);
		}
		return differences;
	}

	private async Task<HrPolicyVersionDisplayContext> BuildDisplayContextAsync(params string[] snapshots)
	{
		var ids = new HashSet<Guid>();
		foreach (var snapshot in snapshots.Where(x => !string.IsNullOrWhiteSpace(x)))
		{
			using var doc = HrPolicyVersionDiffBuilder.ParseSnapshot(snapshot);
			HrPolicyVersionDiffBuilder.CollectGuids(doc.RootElement, ids);
		}

		var context = new HrPolicyVersionDisplayContext();
		if (ids.Count == 0)
		{
			return context;
		}

		var workCalendars = await AsyncExecuter.ToListAsync((await workCalendarRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in workCalendars)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var payrollPolicies = await AsyncExecuter.ToListAsync((await payrollPolicyRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in payrollPolicies)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var attendancePolicies = await AsyncExecuter.ToListAsync((await attendancePolicyRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in attendancePolicies)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var users = await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var user in users)
		{
			context.UserNames[user.Id] = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
		}

		return context;
	}
}