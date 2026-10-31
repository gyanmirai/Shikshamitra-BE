using ShikshaMitra.Finance;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Timing;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class CollectionPolicyAppService(
	IRepository<CollectionPolicy, Guid> repository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<StudentFeePolicyOverride, Guid> overrideRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IAcademicYearRepository academicYearRepository,
	LateFeeCalculationService lateFeeCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
	FinanceRoutingManager routingManager,
	ITransactionCategoryReferenceLock referenceLock,
	FinanceScopeAccessService scopeAccess,
	IClock clock)
	: ApplicationService, ICollectionPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<PagedResultDto<CollectionPolicyDto>> GetListAsync(CollectionPolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!input.IncludeArchived &&
			!string.Equals(input.Status?.Trim(), "Archived", StringComparison.OrdinalIgnoreCase))
		{
			query = query.Where(x => x.Status != CollectionPolicyStatus.Archived);
		}

		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			var status = CollectionPolicyMapping.ParseStatus(input.Status);
			query = query.Where(x => x.Status == status);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.PolicyName.ToLower().Contains(search));
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
		if (input.EnableLateFee.HasValue)
			query = query.Where(x => x.EnableLateFee == input.EnableLateFee.Value);
		if (Enum.TryParse<LateFeeCalculationType>(input.LateFeeType, true, out var lateFeeType) &&
			Enum.IsDefined(lateFeeType))
			query = query.Where(x => x.LateFeeType == lateFeeType);
		if (input.GraceDaysValue.HasValue)
		{
			var days = input.GraceDaysValue.Value;
			var max = input.GraceDaysMax ?? days;
			query = input.GraceDaysOperator?.ToLowerInvariant() switch
			{
				"greaterthan" => query.Where(x => x.GracePeriodDays > days),
				"lessthan" => query.Where(x => x.GracePeriodDays < days),
				"between" => query.Where(x => x.GracePeriodDays >= days && x.GracePeriodDays <= max),
				_ => query.Where(x => x.GracePeriodDays == days)
			};
		}
		if (input.AllowOnlineCollection.HasValue)
			query = query.Where(x => x.AllowOnlineCollection == input.AllowOnlineCollection.Value);
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
			"enablelatefee" => descending ? query.OrderByDescending(x => x.EnableLateFee) : query.OrderBy(x => x.EnableLateFee),
			"latefeetype" => descending
				? query.OrderByDescending(x => x.LateFeeType == LateFeeCalculationType.Compound ? 0 :
					x.LateFeeType == LateFeeCalculationType.Flat ? 1 :
						x.LateFeeType == LateFeeCalculationType.PerDay ? 2 :
							x.LateFeeType == LateFeeCalculationType.PerMonth ? 3 :
								x.LateFeeType == LateFeeCalculationType.PerWeek ? 4 :
									x.LateFeeType == LateFeeCalculationType.Percentage ? 5 : 6)
				: query.OrderBy(x => x.LateFeeType == LateFeeCalculationType.Compound ? 0 :
					x.LateFeeType == LateFeeCalculationType.Flat ? 1 :
						x.LateFeeType == LateFeeCalculationType.PerDay ? 2 :
							x.LateFeeType == LateFeeCalculationType.PerMonth ? 3 :
								x.LateFeeType == LateFeeCalculationType.PerWeek ? 4 :
									x.LateFeeType == LateFeeCalculationType.Percentage ? 5 : 6),
			"graceperioddays" => descending ? query.OrderByDescending(x => x.GracePeriodDays) : query.OrderBy(x => x.GracePeriodDays),
			"allowonlinecollection" => descending ? query.OrderByDescending(x => x.AllowOnlineCollection) : query.OrderBy(x => x.AllowOnlineCollection),
			"updatedat" => descending ? query.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime) : query.OrderBy(x => x.LastModificationTime ?? x.CreationTime),
			"status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
			_ => query.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime)
		};
		var rows = await AsyncExecuter.ToListAsync(sorted
			.ThenBy(x => x.Id)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var usage = await GetUsageCountsAsync(rows.Select(x => x.Id).ToArray());
		var items = rows.Select(x =>
		{
			var dto = CollectionPolicyMapping.ToDto(x);
			dto.IsUsedInDownstream = usage.Contains(x.Id);
			return dto;
		}).ToList();

		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "collection", items);
		return new PagedResultDto<CollectionPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<CollectionPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetEntityOrThrowAsync(id);
		var dto = CollectionPolicyMapping.ToDto(entity);
		dto.IsUsedInDownstream = (await GetUsageCountsAsync(new[] { id })).Contains(id);
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "collection", new[] { dto });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	[UnitOfWork(isTransactional: true)]
	public async Task<CollectionPolicyDto> CreateAsync(CreateUpdateCollectionPolicyDto input)
	{
		var model = await BuildValidatedModelAsync(input);
		if (model.Status == CollectionPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.Status == CollectionPolicyStatus.Active)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add);
		}
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, input.PolicyCode, null);
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, null, input.PolicyName, input.ChangeReason);

		var entity = new CollectionPolicy(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, model);
		await repository.InsertAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, "created");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	[UnitOfWork(isTransactional: true)]
	public async Task<CollectionPolicyDto> UpdateAsync(Guid id, CreateUpdateCollectionPolicyDto input)
	{
		var entity = await GetEntityOrThrowAsync(id);

		if (entity.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearCannotChange);
		}

		var model = await BuildValidatedModelAsync(input);
		if (entity.Status == CollectionPolicyStatus.Archived)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ArchivedPolicyNotEditable);
		}
		if (input.AllocationBasis is null) model = model with { AllocationBasis = entity.AllocationBasis };
		if (input.ReceiptGrouping is null) model = model with { ReceiptGrouping = entity.ReceiptGrouping };
		if (entity.Status == CollectionPolicyStatus.Active && model.Status == CollectionPolicyStatus.Draft &&
			(await GetUsageCountsAsync(new[] { id })).Contains(id))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyInUse);
		}
		if (model.Status == CollectionPolicyStatus.Active && entity.Status != CollectionPolicyStatus.Active)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add);
		}
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, input.PolicyCode, id);
		if (entity.IsDefault && (!model.IsDefault || model.Status != CollectionPolicyStatus.Active))
			throw new BusinessException(CollectionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if (model.Status == CollectionPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);

		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return dto;
	}

	[Authorize]
	[UnitOfWork(isTransactional: true)]
	public async Task<CollectionPolicyDto> CloneAsync(Guid id, CollectionPolicyCloneRequestDto input)
	{
		await EnsureCanCloneAsync();

		var source = await GetEntityOrThrowAsync(id);
		var sourceDto = CollectionPolicyMapping.ToDto(source);
		var targetAcademicYearId = input.TargetAcademicYearId ?? source.AcademicYearId;
		var sameAcademicYear = targetAcademicYearId == source.AcademicYearId;

		var createDto = new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = targetAcademicYearId,
			PolicyName = string.IsNullOrWhiteSpace(input.Name)
				? FinanceName.NextCopy(source.PolicyName, await AsyncExecuter.ToListAsync(
					(await repository.GetQueryableAsync()).Where(x => x.AcademicYearId == (input.TargetAcademicYearId ?? source.AcademicYearId))
					.Select(x => x.PolicyName)))
				: FinanceName.Normalize(input.Name),
			// Sequential GUID prefixes can repeat. Use random GUID material within the code length limit.
			PolicyCode = $"CP-{Guid.NewGuid():N}"[..27].ToUpperInvariant(),
			Description = source.Description,
			Status = "Draft",
			IsDefault = false,
			EnableLateFee = sourceDto.EnableLateFee,
			GracePeriodDays = sourceDto.GracePeriodDays,
			LateFeeApplyOnFeeHeads = sameAcademicYear ? sourceDto.LateFeeApplyOnFeeHeads : new List<Guid>(),
			LateFeeType = sourceDto.LateFeeType,
			FlatAmount = sourceDto.FlatAmount,
			Percentage = sourceDto.Percentage,
			PercentageBase = sourceDto.PercentageBase,
			AmountPerDay = sourceDto.AmountPerDay,
			AmountPerWeek = sourceDto.AmountPerWeek,
			AmountPerMonth = sourceDto.AmountPerMonth,
			CompoundFrequency = sourceDto.CompoundFrequency,
			EscalationRules = sourceDto.EscalationRules,
			FeeHeadOverrides = sameAcademicYear ? sourceDto.FeeHeadOverrides : new List<CollectionPolicyFeeHeadOverrideDto>(),
			RoundingMethod = sourceDto.RoundingMethod,
			ConsiderAsIncome = sourceDto.ConsiderAsIncome,
			LateFeeIncomeHeadId = null,
			LateFeeIncomeCategoryId = sameAcademicYear ? sourceDto.LateFeeIncomeCategoryId : null,
			MaximumLateFeeAmount = sourceDto.MaximumLateFeeAmount,
			MaximumChargingDays = sourceDto.MaximumChargingDays,
			AllowOnlineCollection = sourceDto.AllowOnlineCollection,
			ReceiptGrouping = sourceDto.ReceiptGrouping,
			AllocationBasis = sourceDto.AllocationBasis
		};

		var model = await BuildValidatedModelAsync(createDto);
		await EnsureUniqueAsync(targetAcademicYearId, createDto.PolicyName, createDto.PolicyCode, null);
		var entity = new CollectionPolicy(GuidGenerator.Create(), CurrentTenant.Id, targetAcademicYearId, model);
		await repository.InsertAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, "cloned");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Delete)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetEntityOrThrowAsync(id);
		if (entity.Status == CollectionPolicyStatus.Archived)
		{
			return;
		}
		if (entity.IsDefault)
			throw new BusinessException(CollectionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);

		if ((await GetUsageCountsAsync(new[] { id })).Contains(id))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyInUse);
		}
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize]
	public async Task<CollectionPolicyDto> UnarchiveAsync(Guid id)
	{
		await EnsureCanUnarchiveAsync();

		var entity = await GetEntityOrThrowAsync(id);
		if (entity.Status != CollectionPolicyStatus.Archived)
		{
			return CollectionPolicyMapping.ToDto(entity);
		}

		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, "unarchived");
		return dto;
	}

	private async Task EnsureCanCloneAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)
			|| await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add))
		{
			return;
		}

		await AuthorizationService.CheckAsync(
			ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add);
	}

	private async Task EnsureCanUnarchiveAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)
			|| await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Delete))
		{
			return;
		}

		await AuthorizationService.CheckAsync(
			ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, FinancePolicyVersionListRequestDto input)
	{
		await GetEntityOrThrowAsync(id);

		var query = await versionRepository.GetQueryableAsync();
		query = query.Where(x => x.PolicyType == "collection" && x.PolicyId == id);

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.VersionNumber)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var items = rows.Select(x => new FinancePolicyVersionDto
		{
			Id = x.Id,
			PolicyType = x.PolicyType,
			PolicyId = x.PolicyId,
			AcademicYearId = x.AcademicYearId,
			VersionNumber = x.VersionNumber,
			PolicyName = x.PolicyName,
			Status = x.Status,
			SnapshotJson = x.SnapshotJson,
			ChangeAction = x.ChangeAction,
			ChangeReason = x.ChangeReason,
			CreationTime = x.CreationTime,
			CreatorId = x.CreatorId
		}).ToList();

		await FinancePolicyVersionCreatorResolver.PopulateUserNamesAsync(items, userRepository);
		return new PagedResultDto<FinancePolicyVersionDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<CollectionPolicySimulateResponseDto> SimulateAsync(Guid id, CollectionPolicySimulateRequestDto input)
	{
		var policy = await GetEntityOrThrowAsync(id);

		var isExempt = input.IsLateFeeExempt;
		var customGracePeriodDays = input.CustomGracePeriodDays;
		if (input.StudentId is { } studentId && input.FeeStructureId is { } feeStructureId)
		{
			var overrideQuery = await overrideRepository.GetQueryableAsync();
			var overrideEntity = overrideQuery.FirstOrDefault(
				o => o.StudentId == studentId && o.FeeStructureId == feeStructureId);
			isExempt = overrideEntity?.IsLateFeeExempt ?? isExempt;
			customGracePeriodDays = overrideEntity?.CustomGracePeriodDays ?? customGracePeriodDays;
		}

		var activeWindow = await exceptionWindowResolver.ResolveActiveWindowAsync(
			policy.AcademicYearId, input.FeeStructureId, clock.Now.Date);

		var lateFeeResult = lateFeeCalculationService.Calculate(policy, new LateFeeCalculationInput(
			input.DaysLate,
			input.OutstandingAmount,
			input.OriginalFeeAmount ?? input.OutstandingAmount,
			input.RemainingBalance ?? input.OutstandingAmount,
			input.FeeHeadId,
			isExempt,
			customGracePeriodDays,
			activeWindow?.Effect,
			activeWindow?.ExtensionDays,
			activeWindow?.Label));

		var response = new CollectionPolicySimulateResponseDto
		{
			LateFee = lateFeeResult.LateFee,
			IsExempt = lateFeeResult.IsExempt,
			EffectiveDaysLate = lateFeeResult.EffectiveDaysLate,
			EffectiveGracePeriodDays = lateFeeResult.EffectiveGracePeriodDays,
			ExceptionWindowApplied = lateFeeResult.ExceptionWindowApplied,
			FeeHeadOverrideApplied = lateFeeResult.FeeHeadOverrideApplied,
			AmountBeforeCap = lateFeeResult.AmountBeforeCap,
			CappedBy = lateFeeResult.CappedBy,
			RoundingApplied = lateFeeResult.RoundingApplied,
			Explanation = lateFeeResult.Explanation
		};

		return response;
	}

	private async Task<CollectionPolicy> GetEntityOrThrowAsync(Guid id)
	{
		var entity = await repository.FindAsync(id);
		if (entity is null)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotFound).WithData("id", id);
		}

		return entity;
	}

	/// <summary>
	/// Transfers the academic-year default before the selected policy is saved. The unit of work
	/// keeps demotion and promotion atomic, while the filtered unique index remains the final guard.
	/// </summary>
	private async Task ReplaceExistingDefaultAsync(
		Guid academicYearId, Guid? excludeId, string replacementName, string? reason)
	{
		var existingDefaults = await repository.GetListAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			(excludeId == null || x.Id != excludeId));

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
			x.Status == CollectionPolicyStatus.Active && x.IsDefault);

	private async Task EnsureUniqueAsync(Guid academicYearId, string name, string code, Guid? excludeId)
	{
		var normalizedName = FinanceName.Normalize(name);
		var normalizedCode = code.Trim().ToUpperInvariant();
		var query = await repository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(query.Where(x => x.AcademicYearId == academicYearId &&
			(!excludeId.HasValue || x.Id != excludeId.Value)).Select(x => new { x.PolicyName, x.Status }));
		var existing = candidates.FirstOrDefault(x => FinanceName.Equals(x.PolicyName, normalizedName));
		if (existing != null)
			throw new BusinessException(CollectionPolicyErrorCodes.DuplicateName)
				.WithData("Name", normalizedName)
				.WithData("Location", existing.Status == CollectionPolicyStatus.Archived ? "Archived" : "Current");
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == academicYearId &&
			x.PolicyCode.ToUpper() == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(CollectionPolicyErrorCodes.DuplicatePolicyCode).WithData("Code", normalizedCode);
	}

	private async Task<CollectionPolicyUpdateModel> BuildValidatedModelAsync(CreateUpdateCollectionPolicyDto input)
	{
		await scopeAccess.EnsureAcademicContextAccessibleAsync(input.AcademicYearId, null, null, null, null);
		if (input.LateFeeIncomeCategoryId.HasValue || (input.EnableLateFee && input.Status == "Active"))
		{
			await referenceLock.AcquireAsync();
			await routingManager.RequireCategoryAsync(input.AcademicYearId, input.LateFeeIncomeCategoryId, TransactionType.Income);
		}
		var year = await academicYearRepository.FindAsync(input.AcademicYearId)
			?? throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearRequired);
		var model = CollectionPolicyMapping.ToUpdateModel(input, year.StartDate.Date, year.EndDate.Date);
		var feeHeadIds = model.LateFeeApplyOnFeeHeads
			.Concat(model.FeeHeadOverrides.Select(x => x.FeeHeadId))
			.Distinct().ToList();
		if (feeHeadIds.Count > 0)
		{
			var heads = await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
			if (heads.Count != feeHeadIds.Count || heads.Any(x => x.AcademicYearId != input.AcademicYearId || !x.IsActive))
			{
				throw new BusinessException(CollectionPolicyErrorCodes.InvalidFeeHeadReference);
			}

			var categoryIds = heads.Select(x => x.FeeCategoryId).Distinct().ToList();
			var activeCategoryCount = await feeCategoryRepository.CountAsync(x =>
				categoryIds.Contains(x.Id) &&
				x.AcademicYearId == input.AcademicYearId &&
				x.IsActive);
			if (activeCategoryCount != categoryIds.Count)
			{
				throw new BusinessException(CollectionPolicyErrorCodes.InvalidFeeHeadReference)
					.WithData("reason", "inactive_fee_category");
			}
		}

		return model;
	}

	private async Task<HashSet<Guid>> GetUsageCountsAsync(Guid[] policyIds)
	{
		if (policyIds.Length == 0)
		{
			return new HashSet<Guid>();
		}

		// Deliberately a List<Guid>, not the Guid[] parameter directly: EF Core's query
		// translator can choke on Array.Contains under recent .NET's ReadOnlySpan-based
		// overload resolution ("violates the constraint of type parameter 'TRet'"),
		// while List<T>.Contains translates to a parameterized IN (...) reliably.
		var policyIdList = policyIds.ToList();
		var query = await feeStructureRepository.GetQueryableAsync();
		var used = await AsyncExecuter.ToListAsync(query
			.Where(x => x.CollectionPolicyId != null && policyIdList.Contains(x.CollectionPolicyId!.Value))
			.Select(x => x.CollectionPolicyId!.Value)
			.Distinct());

		return used.ToHashSet();
	}

	private async Task WriteVersionAsync(
		CollectionPolicy entity,
		string changeAction,
		string? changeReason = null)
	{
		var version = new FinancePolicyVersion(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			"collection",
			entity.Id,
			entity.AcademicYearId,
			entity.Version,
			entity.PolicyName,
			CollectionPolicyMapping.MapStatus(entity.Status),
			entity.ToSnapshot().Serialize(),
			changeAction,
			changeReason);

		await versionRepository.InsertAsync(version, autoSave: true);
	}
}
