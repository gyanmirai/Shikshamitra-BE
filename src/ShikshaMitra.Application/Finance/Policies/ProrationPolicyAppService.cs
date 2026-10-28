using ShikshaMitra.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class ProrationPolicyAppService(
	IRepository<ProrationPolicy, Guid> repository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
	ProrationPolicyCalculationService calculationService)
	: ApplicationService, IProrationPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View)]
	public async Task<PagedResultDto<ProrationPolicyDto>> GetListAsync(ProrationPolicyListRequestDto input)
	{
		var query = await repository.WithDetailsAsync(x => x.Rules);
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			var status = ParseListStatus(input.Status);
			query = query.Where(x => x.Status == status);
		}
		else
		{
			query = query.Where(x => x.Status != ProrationPolicyStatus.Archived);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.PolicyName.ToLower().Contains(search) || x.PolicyCode.ToLower().Contains(search));
		}
		if (!string.IsNullOrWhiteSpace(input.PolicyNameFilter))
		{
			var name = input.PolicyNameFilter.Trim().ToLower();
			query = input.PolicyNameOperator?.ToLowerInvariant() switch
			{
				"equals" => query.Where(x => x.PolicyName.ToLower() == name),
				"startswith" => query.Where(x => x.PolicyName.ToLower().StartsWith(name)),
				"endswith" => query.Where(x => x.PolicyName.ToLower().EndsWith(name)),
				_ => query.Where(x => x.PolicyName.ToLower().Contains(name))
			};
		}
		if (input.RuleCountValue.HasValue)
		{
			var count = input.RuleCountValue.Value;
			var max = input.RuleCountMax ?? count;
			query = input.RuleCountOperator?.ToLowerInvariant() switch
			{
				"greaterthan" => query.Where(x => x.Rules.Count > count),
				"lessthan" => query.Where(x => x.Rules.Count < count),
				"between" => query.Where(x => x.Rules.Count >= count && x.Rules.Count <= max),
				_ => query.Where(x => x.Rules.Count == count)
			};
		}
		if (input.UpdatedAt.HasValue && input.UpdatedAtEnd.HasValue)
		{
			var start = input.UpdatedAt.Value;
			var end = input.UpdatedAtEnd.Value;
			query = input.UpdatedAtOperator?.ToLowerInvariant() switch
			{
				"after" => query.Where(x => (x.LastModificationTime ?? x.CreationTime) >= start),
				"before" => query.Where(x => (x.LastModificationTime ?? x.CreationTime) < end),
				_ => query.Where(x => (x.LastModificationTime ?? x.CreationTime) >= start &&
					(x.LastModificationTime ?? x.CreationTime) < end)
			};
		}

		var total = await AsyncExecuter.CountAsync(query);
		var sortParts = input.Sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
		var sortColumn = sortParts.Length > 0 ? sortParts[0].ToLowerInvariant() : string.Empty;
		var descending = sortParts.Length > 1 && sortParts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		var sorted = sortColumn switch
		{
			"policyname" => descending ? query.OrderByDescending(x => x.PolicyName) : query.OrderBy(x => x.PolicyName),
			"feeheadrules" => descending ? query.OrderByDescending(x => x.Rules.Count) : query.OrderBy(x => x.Rules.Count),
			"updatedat" => descending ? query.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime) : query.OrderBy(x => x.LastModificationTime ?? x.CreationTime),
			"status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
			_ => query.OrderByDescending(x => x.Status == ProrationPolicyStatus.Active)
				.ThenByDescending(x => x.IsDefault)
				.ThenByDescending(x => x.CreationTime)
		};
		var rows = await AsyncExecuter.ToListAsync(sorted
			.ThenBy(x => x.Id)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));
		var usedIds = await GetUsedIdsAsync(rows.Select(x => x.Id).ToList());
		var academicYearName = (await academicYearRepository.GetAsync(input.AcademicYearId)).Name;
		var names = await GetFeeHeadNamesAsync(rows.SelectMany(x => x.Rules).Select(x => x.FeeHeadId));
		var items = rows.Select(x =>
		{
			var dto = ProrationPolicyMapping.ToDto(x, names);
			dto.AcademicYearName = academicYearName;
			dto.IsUsedInDownstream = usedIds.Contains(x.Id);
			return dto;
		}).ToList();
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "proration", items);
		return new PagedResultDto<ProrationPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View)]
	public async Task<ProrationPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		var dto = await ToDtoAsync(entity);
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "proration", new[] { dto });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View)]
	public async Task<ProrationPolicyImpactDto> GetImpactAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		return new ProrationPolicyImpactDto
		{
			PolicyId = entity.Id,
			PolicyName = entity.PolicyName,
			Status = entity.Status.ToString(),
			CurrentVersion = entity.Version,
			AttachedFeeStructureCount = await feeStructureRepository.CountAsync(x => x.ProrationPolicyId == id),
			StudentAssignmentCount = await studentFeeStructureRepository.CountAsync(x => x.ProrationPolicyId == id),
			ChangeReasonRequired = entity.Status == ProrationPolicyStatus.Active
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add)]
	public async Task<ProrationPolicyDto> CreateAsync(CreateUpdateProrationPolicyDto input)
	{
		if (ProrationPolicyMapping.ParseStatus(input.Status) == ProrationPolicyStatus.Active)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add);
		await academicYearRepository.GetAsync(input.AcademicYearId);
		var code = string.IsNullOrWhiteSpace(input.PolicyCode)
			? $"PRO-{Guid.NewGuid():N}"[..28].ToUpperInvariant()
			: input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, null);
		await ValidateReferencesAsync(input.AcademicYearId, input.FeeHeadRules.Select(x => x.FeeHeadId));
		var model = ProrationPolicyMapping.ToUpdateModel(input, code, GuidGenerator.Create);
		if (model.Status == ProrationPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, null, input.PolicyName, input.ChangeReason);
		var entity = new ProrationPolicy(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, model);
		await repository.InsertAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "created", input.ChangeReason);
		return await ToDtoAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add)]
	public async Task<ProrationPolicyDto> UpdateAsync(Guid id, CreateUpdateProrationPolicyDto input)
	{
		var entity = await GetRequiredAsync(id);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (entity.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(ProrationPolicyErrorCodes.AcademicYearCannotChange);
		if (entity.Status == ProrationPolicyStatus.Active && string.IsNullOrWhiteSpace(input.ChangeReason))
			throw new BusinessException(ProrationPolicyErrorCodes.ChangeReasonRequired);
		var targetStatus = ProrationPolicyMapping.ParseStatus(input.Status);
		if (entity.Status != ProrationPolicyStatus.Active && targetStatus == ProrationPolicyStatus.Active)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add);
		if (entity.Status == ProrationPolicyStatus.Active && targetStatus == ProrationPolicyStatus.Draft &&
			(await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyInUse);

		var code = string.IsNullOrWhiteSpace(input.PolicyCode) ? entity.PolicyCode : input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, id);
		await ValidateReferencesAsync(input.AcademicYearId, input.FeeHeadRules.Select(x => x.FeeHeadId));
		var model = ProrationPolicyMapping.ToUpdateModel(input, code, GuidGenerator.Create);
		if (entity.IsDefault && (!model.IsDefault || model.Status != ProrationPolicyStatus.Active))
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if (model.Status == ProrationPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);
		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return await ToDtoAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add)]
	public async Task<ProrationPolicyDto> ActivateAsync(Guid id, string? changeReason = null)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ProrationPolicyStatus.Active)
			return await ToDtoAsync(entity);
		entity.Activate();
		if (!await HasActiveDefaultAsync(entity.AcademicYearId))
			entity.SetDefault(true);
		if (entity.IsDefault)
			await ReplaceExistingDefaultAsync(entity.AcademicYearId, id, entity.PolicyName, changeReason);
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "activated", changeReason);
		return await ToDtoAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add)]
	public async Task<ProrationPolicyDto> CloneAsync(Guid id, ProrationPolicyCloneRequestDto input)
	{
		var source = await GetRequiredAsync(id);
		var targetAcademicYearId = input.TargetAcademicYearId ?? source.AcademicYearId;
		return await CreateAsync(new CreateUpdateProrationPolicyDto
		{
			AcademicYearId = targetAcademicYearId,
			PolicyName = string.IsNullOrWhiteSpace(input.Name)
				? FinanceName.NextCopy(source.PolicyName, await AsyncExecuter.ToListAsync(
					(await repository.GetQueryableAsync()).Where(x => x.AcademicYearId == targetAcademicYearId)
					.Select(x => x.PolicyName)))
				: FinanceName.Normalize(input.Name),
			Description = source.Description,
			Status = "Draft",
			IsDefault = false,
			FeeHeadRules = source.Rules.Select(x => new ProrationPolicyRuleDto
			{
				FeeHeadId = x.FeeHeadId,
				RuleType = x.RuleType.ToString(),
				RoundingMode = x.RoundingMode.ToString(),
				MinimumChargeDays = x.MinimumChargeDays,
				CutoffDay = x.CutoffDay,
				AfterCutoffAction = x.AfterCutoffAction?.ToString(),
				ApplyOnAdmission = x.ApplyOnAdmission,
				ApplyOnEarlyWithdrawal = x.ApplyOnEarlyWithdrawal,
				EarlyWithdrawalRuleType = x.EarlyWithdrawalRuleType?.ToString(),
				EarlyWithdrawalMinimumChargeDays = x.EarlyWithdrawalMinimumChargeDays,
				EarlyWithdrawalCutoffDay = x.EarlyWithdrawalCutoffDay,
				EarlyWithdrawalBeforeCutoffAction = x.EarlyWithdrawalBeforeCutoffAction?.ToString()
			}).ToList()
		});
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Delete)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ProrationPolicyStatus.Archived) return;
		if (entity.IsDefault)
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if ((await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyInUse);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add)]
	public async Task<ProrationPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ProrationPolicyStatus.Archived)
		{
			entity.Unarchive();
			await repository.UpdateAsync(entity, autoSave: true);
			await WriteVersionAsync(entity, "unarchived");
		}
		return await ToDtoAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View)]
	public async Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, FinancePolicyVersionListRequestDto input)
	{
		await GetRequiredAsync(id);
		var query = (await versionRepository.GetQueryableAsync()).Where(x => x.PolicyType == "proration" && x.PolicyId == id);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.VersionNumber)
			.Skip(input.SkipCount).Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));
		var items = rows.Select(x => new FinancePolicyVersionDto
		{
			Id = x.Id, PolicyType = x.PolicyType, PolicyId = x.PolicyId,
			AcademicYearId = x.AcademicYearId, VersionNumber = x.VersionNumber,
			PolicyName = x.PolicyName, Status = x.Status, SnapshotJson = x.SnapshotJson,
			ChangeAction = x.ChangeAction, ChangeReason = x.ChangeReason,
			CreationTime = x.CreationTime, CreatorId = x.CreatorId
		}).ToList();
		await FinancePolicyVersionCreatorResolver.PopulateUserNamesAsync(items, userRepository);
		return new PagedResultDto<FinancePolicyVersionDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View)]
	public async Task<ProrationCalculationResultDto> SimulateAsync(Guid id, ProrationPolicySimulateRequestDto input)
	{
		var entity = await GetRequiredAsync(id);
		var rule = entity.Rules.FirstOrDefault(x => x.FeeHeadId == input.FeeHeadId)?.ToSnapshot()
			?? new ProrationPolicyRuleSnapshot(input.FeeHeadId, ProrationRuleType.FullCharge,
				ProrationRoundingMode.Currency, null, null, null);
		var result = calculationService.Calculate(rule, input.ScheduledAmount, input.CoverageStartDate,
			input.CoverageEndDate, input.ChargeStartDate, input.ChargeEndDate);
		return ToCalculationDto(entity, input.FeeHeadId, result);
	}

	private async Task<ProrationPolicy> GetRequiredAsync(Guid id)
	{
		var query = await repository.WithDetailsAsync(x => x.Rules);
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyNotFound).WithData("id", id);
	}

	private async Task<ProrationPolicyDto> ToDtoAsync(ProrationPolicy entity)
	{
		var dto = ProrationPolicyMapping.ToDto(
			entity,
			await GetFeeHeadNamesAsync(entity.Rules.Select(x => x.FeeHeadId)));
		dto.AcademicYearName = (await academicYearRepository.GetAsync(entity.AcademicYearId)).Name;
		dto.IsUsedInDownstream = (await GetUsedIdsAsync([entity.Id])).Contains(entity.Id);
		return dto;
	}

	private async Task ValidateReferencesAsync(Guid academicYearId, IEnumerable<Guid> feeHeadIds)
	{
		var ids = feeHeadIds.Distinct().ToList();
		var count = await feeHeadRepository.CountAsync(x =>
			ids.Contains(x.Id) && x.IsActive && x.AcademicYearId == academicYearId);
		if (count != ids.Count)
		{
			var unavailable = await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id) &&
				(!x.IsActive || x.AcademicYearId != academicYearId));
			var names = string.Join(", ", unavailable.Select(x => x.Name).OrderBy(x => x));
			throw new BusinessException(ProrationPolicyErrorCodes.FeeHeadUnavailable)
				.WithData("FeeHeadNames", string.IsNullOrWhiteSpace(names) ? "One or more selected fee heads" : names)
				.WithData("AcademicYearId", academicYearId);
		}
	}

	private async Task<Dictionary<Guid, string>> GetFeeHeadNamesAsync(IEnumerable<Guid> ids)
	{
		var values = ids.Distinct().ToList();
		if (values.Count == 0) return [];
		return (await feeHeadRepository.GetListAsync(x => values.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
	}

	private async Task EnsureUniqueAsync(Guid yearId, string name, string code, Guid? excludeId)
	{
		var normalizedName = FinanceName.Normalize(name);
		var normalizedCode = code.Trim().ToUpperInvariant();
		var query = await repository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(query.Where(x => x.AcademicYearId == yearId &&
			(!excludeId.HasValue || x.Id != excludeId.Value)).Select(x => new { x.PolicyName, x.Status }));
		var existing = candidates.FirstOrDefault(x => FinanceName.Equals(x.PolicyName, normalizedName));
		if (existing != null)
			throw new BusinessException(ProrationPolicyErrorCodes.DuplicateName)
				.WithData("Name", normalizedName)
				.WithData("Location", existing.Status == ProrationPolicyStatus.Archived ? "Archived" : "Current");
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyCode.ToUpper() == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(ProrationPolicyErrorCodes.DuplicateCode).WithData("Code", normalizedCode);
	}

	private async Task ReplaceExistingDefaultAsync(
		Guid yearId, Guid? excludeId, string replacementName, string? reason)
	{
		var existingDefaults = await repository.GetListAsync(x =>
			x.AcademicYearId == yearId && x.IsDefault &&
			(!excludeId.HasValue || x.Id != excludeId.Value));
		foreach (var existingDefault in existingDefaults)
		{
			existingDefault.SetDefault(false);
			await repository.UpdateAsync(existingDefault, autoSave: true);
			await WriteVersionAsync(
				existingDefault,
				"default_replaced",
				reason ?? $"Default replaced by {replacementName}.");
		}
	}

	private async Task<bool> HasActiveDefaultAsync(Guid academicYearId) =>
		await repository.AnyAsync(x => x.AcademicYearId == academicYearId &&
			x.Status == ProrationPolicyStatus.Active && x.IsDefault);

	private async Task<HashSet<Guid>> GetUsedIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0) return [];
		var structureIds = await feeStructureRepository.GetListAsync(x => x.ProrationPolicyId.HasValue && ids.Contains(x.ProrationPolicyId.Value));
		var assignmentIds = await studentFeeStructureRepository.GetListAsync(x => x.ProrationPolicyId.HasValue && ids.Contains(x.ProrationPolicyId.Value));
		return structureIds.Select(x => x.ProrationPolicyId!.Value)
			.Concat(assignmentIds.Select(x => x.ProrationPolicyId!.Value)).ToHashSet();
	}

	private async Task WriteVersionAsync(ProrationPolicy entity, string action, string? reason = null)
	{
		await versionRepository.InsertAsync(new FinancePolicyVersion(
			GuidGenerator.Create(), CurrentTenant.Id, "proration", entity.Id,
			entity.AcademicYearId, entity.Version, entity.PolicyName, entity.Status.ToString(),
			ProrationPolicyMapping.ToSnapshotJson(entity), action, reason), autoSave: true);
	}

	private static void EnsureConcurrency(ProrationPolicy entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp))
			throw new BusinessException(ProrationPolicyErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, stamp, StringComparison.Ordinal))
			throw new BusinessException(ProrationPolicyErrorCodes.ConcurrencyConflict);
	}

	private static ProrationPolicyStatus ParseListStatus(string value) => value.Trim().ToLowerInvariant() switch
	{
		"draft" => ProrationPolicyStatus.Draft,
		"active" => ProrationPolicyStatus.Active,
		"archived" => ProrationPolicyStatus.Archived,
		_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidStatus)
	};

	private static ProrationCalculationResultDto ToCalculationDto(
		ProrationPolicy policy, Guid feeHeadId, ProrationCalculationResult result) => new()
	{
		FeeHeadId = feeHeadId,
		ScheduledAmount = result.ScheduledAmount,
		ProratedAmount = result.ProratedAmount,
		ReductionAmount = result.ScheduledAmount - result.ProratedAmount,
		RawAmount = result.RawAmount,
		Factor = result.Factor,
		CoverageStartDate = result.CoverageStartDate,
		CoverageEndDate = result.CoverageEndDate,
		EligibleStartDate = result.EligibleStartDate,
		EligibleEndDate = result.EligibleEndDate,
		CoverageDays = result.CoverageDays,
		ActualEligibleDays = result.ActualEligibleDays,
		ChargedDays = result.ChargedDays,
		RuleType = result.RuleType.ToString(),
		RoundingMode = result.RoundingMode.ToString(),
		Explanation = result.Explanation,
		PolicyId = policy.Id,
		PolicyVersion = policy.Version
	};
}
