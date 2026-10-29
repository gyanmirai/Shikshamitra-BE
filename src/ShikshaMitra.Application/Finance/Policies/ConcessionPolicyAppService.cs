using ShikshaMitra.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class ConcessionPolicyAppService(
	IRepository<ConcessionPolicy, Guid> repository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<ConcessionRequest, Guid> concessionRequestRepository,
	IRepository<IdentityUser, Guid> userRepository,
	ConcessionPolicyCalculationService calculationService)
	: ApplicationService, IConcessionPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<PagedResultDto<ConcessionPolicyDto>> GetListAsync(ConcessionPolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			var status = ParseListStatus(input.Status);
			query = query.Where(x => x.Status == status);
		}
		else if (!input.IncludeArchived)
		{
			query = query.Where(x => x.Status != ConcessionPolicyStatus.Archived);
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
		if (!string.IsNullOrWhiteSpace(input.ValueSource))
		{
			var valueSource = ConcessionPolicyMapping.ParseValueSource(input.ValueSource);
			query = query.Where(x => x.ValueSource == valueSource);
		}
		if (!string.IsNullOrWhiteSpace(input.CalculationMethod))
		{
			var method = ConcessionPolicyMapping.ParseCalculationMethod(input.CalculationMethod);
			query = query.Where(x => x.CalculationMethod == method);
		}
		if (!string.IsNullOrWhiteSpace(input.ApplyOn))
		{
			var applyOn = ConcessionPolicyMapping.ParseApplyOn(input.ApplyOn);
			query = query.Where(x => x.ApplyOn == applyOn);
		}
		if (input.LimitOperator is not null && input.LimitValue.HasValue)
		{
			var limit = input.LimitValue.Value;
			var max = input.LimitMax ?? limit;
			// Match the numeric value shown in the table. Calculation Method identifies its unit.
			query = input.LimitOperator.ToLowerInvariant() switch
			{
				"equals" => query.Where(x => (x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
					x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)) == limit),
				"greaterthan" => query.Where(x => (x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
					x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)) > limit),
				"lessthan" => query.Where(x => (x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
					x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)) < limit),
				"between" => query.Where(x => (x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
					x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
						(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)) >= limit &&
					(x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
						x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)) <= max),
				_ => query
			};
		}
		if (input.UpdatedAtOperator is not null && input.UpdatedAt.HasValue && input.UpdatedAtEnd.HasValue)
		{
			var start = input.UpdatedAt.Value;
			var end = input.UpdatedAtEnd.Value;
			query = input.UpdatedAtOperator.ToLowerInvariant() switch
			{
				"equals" => query.Where(x => (x.LastModificationTime ?? x.CreationTime) >= start &&
					(x.LastModificationTime ?? x.CreationTime) < end),
				"after" => query.Where(x => (x.LastModificationTime ?? x.CreationTime) >= start),
				"before" => query.Where(x => (x.LastModificationTime ?? x.CreationTime) < end),
				_ => query
			};
		}

		var total = await AsyncExecuter.CountAsync(query);
		var sortParts = input.Sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
		var sortColumn = sortParts.Length > 0 ? sortParts[0].ToLowerInvariant() : string.Empty;
		var descending = sortParts.Length > 1 && sortParts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		var sorted = sortColumn switch
		{
			"policyname" => descending ? query.OrderByDescending(x => x.PolicyName) : query.OrderBy(x => x.PolicyName),
			"valuesource" => descending
				? query.OrderByDescending(x => x.ValueSource == ConcessionValueSource.PolicyFixed)
				: query.OrderBy(x => x.ValueSource == ConcessionValueSource.PolicyFixed),
			"calculationmethod" => descending
				? query.OrderByDescending(x => x.CalculationMethod == ConcessionCalculationMethod.FixedAmount ? 0 :
					x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 1 : 2)
				: query.OrderBy(x => x.CalculationMethod == ConcessionCalculationMethod.FixedAmount ? 0 :
					x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 1 : 2),
			"policylimit" => descending
				? query.OrderByDescending(x => x.CalculationMethod == ConcessionCalculationMethod.FixedAmount)
					.ThenByDescending(x => (double?)(x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
						x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount)))
				: query.OrderBy(x => x.CalculationMethod == ConcessionCalculationMethod.FixedAmount)
					.ThenBy(x => (double?)(x.CalculationMethod == ConcessionCalculationMethod.FullWaiver ? 100m :
						x.CalculationMethod == ConcessionCalculationMethod.Percentage ?
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.PercentageValue : x.MaximumPercentage) :
							(x.ValueSource == ConcessionValueSource.PolicyFixed ? x.FixedAmount : x.MaximumFixedAmount))),
			"applyon" => descending
				? query.OrderByDescending(x => x.ApplyOn == ConcessionApplyOn.AllFees ? 0 :
					x.ApplyOn == ConcessionApplyOn.AllOutstanding ? 1 :
						x.ApplyOn == ConcessionApplyOn.CurrentInstallment ? 2 : 3)
				: query.OrderBy(x => x.ApplyOn == ConcessionApplyOn.AllFees ? 0 :
					x.ApplyOn == ConcessionApplyOn.AllOutstanding ? 1 :
						x.ApplyOn == ConcessionApplyOn.CurrentInstallment ? 2 : 3),
			"updatedat" => descending ? query.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime) : query.OrderBy(x => x.LastModificationTime ?? x.CreationTime),
			"status" => descending
				? query.OrderByDescending(x => x.Status == ConcessionPolicyStatus.Active ? 0 :
					x.Status == ConcessionPolicyStatus.Archived ? 1 : 2)
				: query.OrderBy(x => x.Status == ConcessionPolicyStatus.Active ? 0 :
					x.Status == ConcessionPolicyStatus.Archived ? 1 : 2),
			_ => query.OrderByDescending(x => x.IsDefault)
				.ThenByDescending(x => x.Status == ConcessionPolicyStatus.Active)
				.ThenByDescending(x => x.CreationTime)
		};
		var rows = await AsyncExecuter.ToListAsync(sorted
			.ThenBy(x => x.Id)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));
		var usedIds = await GetUsedIdsAsync(rows.Select(x => x.Id).ToList());
		var items = rows.Select(x =>
		{
			var dto = ConcessionPolicyMapping.ToDto(x);
			dto.IsUsedInDownstream = usedIds.Contains(x.Id);
			return dto;
		}).ToList();
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "concession", items);
		return new PagedResultDto<ConcessionPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<ConcessionPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		var dto = ConcessionPolicyMapping.ToDto(entity);
		dto.IsUsedInDownstream = (await GetUsedIdsAsync([id])).Contains(id);
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "concession", new[] { dto });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<ConcessionPolicyImpactDto> GetImpactAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		return new ConcessionPolicyImpactDto
		{
			PolicyId = entity.Id,
			PolicyName = entity.PolicyName,
			Status = entity.Status.ToString(),
			CurrentVersion = entity.Version,
			AttachedFeeStructureCount = 0,
			StudentAssignmentCount = 0,
			ConcessionRequestCount = await concessionRequestRepository.CountAsync(x => x.ConcessionPolicyId == id),
			ChangeReasonRequired = entity.Status == ConcessionPolicyStatus.Active,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add)]
	public async Task<ConcessionPolicyDto> CreateAsync(CreateUpdateConcessionPolicyDto input)
	{
		var code = string.IsNullOrWhiteSpace(input.PolicyCode)
			? $"CON-{Guid.NewGuid():N}"[..28].ToUpperInvariant()
			: input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, null);
		var model = ConcessionPolicyMapping.ToUpdateModel(input, code);
		if (model.Status == ConcessionPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		var entity = new ConcessionPolicy(
			GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			model);
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, null, input.PolicyName, input.ChangeReason);
		await repository.InsertAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "created");
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add)]
	public async Task<ConcessionPolicyDto> UpdateAsync(Guid id, CreateUpdateConcessionPolicyDto input)
	{
		var entity = await GetRequiredAsync(id);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (entity.Status == ConcessionPolicyStatus.Archived)
			throw new BusinessException(ConcessionPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (entity.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearCannotChange);
		var targetStatus = ConcessionPolicyMapping.ParseStatus(input.Status);
		if (entity.Status == ConcessionPolicyStatus.Active && targetStatus == ConcessionPolicyStatus.Draft &&
			(await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyInUse);
		if (entity.Status == ConcessionPolicyStatus.Active && string.IsNullOrWhiteSpace(input.ChangeReason))
			throw new BusinessException(ConcessionPolicyErrorCodes.ChangeReasonRequired);

		var code = string.IsNullOrWhiteSpace(input.PolicyCode) ? entity.PolicyCode : input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, id);
		if (entity.IsDefault && (!input.IsDefault || targetStatus != ConcessionPolicyStatus.Active))
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		var model = ConcessionPolicyMapping.ToUpdateModel(input, code);
		if (model.Status == ConcessionPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);
		// Save the demotion before modifying the tracked replacement: autoSave flushes
		// all tracked changes, and the unique index must never see two active defaults.
		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add)]
	public async Task<ConcessionPolicyDto> CloneAsync(Guid id, ConcessionPolicyCloneRequestDto input)
	{
		var source = await GetRequiredAsync(id);
		var targetYear = input.TargetAcademicYearId ?? source.AcademicYearId;
		var dto = new CreateUpdateConcessionPolicyDto
		{
			AcademicYearId = targetYear,
			PolicyName = string.IsNullOrWhiteSpace(input.Name)
				? FinanceName.NextCopy(source.PolicyName, await AsyncExecuter.ToListAsync(
					(await repository.GetQueryableAsync()).Where(x => x.AcademicYearId == (input.TargetAcademicYearId ?? source.AcademicYearId))
					.Select(x => x.PolicyName)))
				: FinanceName.Normalize(input.Name),
			Status = "Draft",
			IsDefault = false,
			Description = source.Description,
			ValueSource = source.ValueSource.ToString(),
			CalculationMethod = source.CalculationMethod.ToString(),
			PercentageValue = source.PercentageValue,
			FixedAmount = source.FixedAmount,
			MaximumPercentage = source.MaximumPercentage,
			MaximumFixedAmount = source.MaximumFixedAmount,
			ApplyOn = source.ApplyOn.ToString(),
			AutoCreateRefund = source.AutoCreateRefund,
			RoundingMethod = source.RoundingMethod.ToString(),
			ApplicableConcessionTypeIds = [],
			FeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure.ToString(),
			ApplicableFeeHeadIds = []
		};
		return await CreateAsync(dto);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Delete)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ConcessionPolicyStatus.Archived) return;
		if (entity.IsDefault)
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if ((await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyInUse);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add)]
	public async Task<ConcessionPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status != ConcessionPolicyStatus.Archived) return ConcessionPolicyMapping.ToDto(entity);
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "unarchived");
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(
		Guid id, FinancePolicyVersionListRequestDto input)
	{
		await GetRequiredAsync(id);
		var query = (await versionRepository.GetQueryableAsync())
			.Where(x => x.PolicyType == "concession" && x.PolicyId == id);
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<ConcessionPolicySimulateResponseDto> SimulateAsync(Guid id, ConcessionPolicySimulateRequestDto input)
	{
		var entity = await GetRequiredAsync(id);
		var amount = calculationService.Calculate(
			entity.ToSnapshot(), input.EligibleBase,
			ConcessionPolicyMapping.ParseRequestedBasis(input.RequestedBasis), input.RequestedValue);
		return new ConcessionPolicySimulateResponseDto
		{
			EligibleBase = input.EligibleBase,
			ConcessionAmount = amount,
			NewOutstanding = Math.Max(0, input.EligibleBase - amount),
			Explanation = $"{entity.ValueSource} / {entity.CalculationMethod}; {entity.RoundingMethod} rounding."
		};
	}

	private async Task<ConcessionPolicy> GetRequiredAsync(Guid id) =>
		await repository.FindAsync(id) ?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotFound).WithData("id", id);


	private async Task EnsureUniqueAsync(Guid yearId, string name, string code, Guid? excludeId)
	{
		var normalizedName = FinanceName.Normalize(name);
		var normalizedCode = code.Trim().ToUpperInvariant();
		var query = await repository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(query.Where(x => x.AcademicYearId == yearId &&
			(!excludeId.HasValue || x.Id != excludeId.Value)).Select(x => new { x.PolicyName, x.Status }));
		var existing = candidates.FirstOrDefault(x => FinanceName.Equals(x.PolicyName, normalizedName));
		if (existing != null)
			throw new BusinessException(ConcessionPolicyErrorCodes.DuplicateName)
				.WithData("Name", normalizedName)
				.WithData("Location", existing.Status == ConcessionPolicyStatus.Archived ? "Archived" : "Current");
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyCode.ToUpper() == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(ConcessionPolicyErrorCodes.DuplicateCode).WithData("Code", normalizedCode);
	}

	private async Task<HashSet<Guid>> GetUsedIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0) return [];
		var query = await concessionRequestRepository.GetQueryableAsync();
		return (await AsyncExecuter.ToListAsync(query
			.Where(x => ids.Contains(x.ConcessionPolicyId))
			.Select(x => x.ConcessionPolicyId).Distinct())).ToHashSet();
	}

	private async Task ReplaceExistingDefaultAsync(
		Guid academicYearId, Guid? excludeId, string replacementName, string? reason)
	{
		var existingDefaults = await repository.GetListAsync(x =>
			x.AcademicYearId == academicYearId && x.IsDefault &&
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
			x.Status == ConcessionPolicyStatus.Active && x.IsDefault);

	private async Task WriteVersionAsync(ConcessionPolicy entity, string action, string? reason = null)
	{
		await versionRepository.InsertAsync(new FinancePolicyVersion(
			GuidGenerator.Create(), CurrentTenant.Id, "concession", entity.Id,
			entity.AcademicYearId, entity.Version, entity.PolicyName, entity.Status.ToString(),
			ConcessionPolicyMapping.ToSnapshotJson(entity), action, reason), autoSave: true);
	}

	private static void EnsureConcurrency(ConcessionPolicy entity, string? concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp))
			throw new BusinessException(ConcessionPolicyErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(ConcessionPolicyErrorCodes.ConcurrencyConflict);
	}

	private static ConcessionPolicyStatus ParseListStatus(string value) => value.Trim().ToLowerInvariant() switch
	{
		"draft" => ConcessionPolicyStatus.Draft,
		"active" => ConcessionPolicyStatus.Active,
		"archived" => ConcessionPolicyStatus.Archived,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidStatus)
	};
}
