using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
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
	IRepository<IdentityRole, Guid> roleRepository,
	LateFeeCalculationService lateFeeCalculationService,
	WaiverCalculationService waiverCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
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

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsDefault)
			.ThenByDescending(x => x.CreationTime)
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
	public async Task<CollectionPolicyDto> CreateAsync(CreateUpdateCollectionPolicyDto input)
	{
		var model = await BuildValidatedModelAsync(input);
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
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);

		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return dto;
	}

	[Authorize]
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
			PolicyName = string.IsNullOrWhiteSpace(input.Name) ? $"{source.PolicyName} Copy" : input.Name!,
			PolicyCode = $"CP-{GuidGenerator.Create().ToString("N")[..8].ToUpperInvariant()}",
			Description = source.Description,
			Status = "Draft",
			IsDefault = false,
			EnableLateFee = sourceDto.EnableLateFee,
			GracePeriodDays = sourceDto.GracePeriodDays,
			GracePeriodType = sourceDto.GracePeriodType,
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
			LateFeeIncomeHeadId = sourceDto.LateFeeIncomeHeadId,
			MaximumLateFeeAmount = sourceDto.MaximumLateFeeAmount,
			MaximumChargingDays = sourceDto.MaximumChargingDays,
			EnableWaiver = sourceDto.EnableWaiver,
			EnableChargeWaiver = sourceDto.EnableChargeWaiver,
			RequiresApproval = sourceDto.RequiresApproval,
			ApprovalRoleIds = sourceDto.ApprovalRoleIds,
			AllowOnlineCollection = sourceDto.AllowOnlineCollection
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

		if (input.RequestedWaiverAmount is { } requestedAmount)
		{
			var requestedFromPercentage = input.RequestedWaiverPercentage is { } pct
				? lateFeeResult.LateFee * pct / 100m
				: (decimal?)null;
			var waiverResult = waiverCalculationService.Calculate(policy, new WaiverCalculationInput(
				requestedFromPercentage ?? requestedAmount,
				lateFeeResult.LateFee,
				Clock.Now,
				FeeHeadId: input.FeeHeadId));

			response.EffectiveWaiverAmount = waiverResult.EffectiveWaiverAmount;
			response.WaiverIsExpired = waiverResult.IsExpired;
			response.WaiverRequiresApproval = waiverResult.RequiresApproval;
			response.WaiverExplanation = waiverResult.Explanation;
		}

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

	private async Task EnsureUniqueAsync(Guid academicYearId, string name, string code, Guid? excludeId)
	{
		var normalizedName = (name ?? string.Empty).Trim().ToLower();
		var normalizedCode = (code ?? string.Empty).Trim().ToUpper();
		var query = await repository.GetQueryableAsync();
		if (query.Any(x => x.AcademicYearId == academicYearId && x.Id != excludeId &&
			(x.PolicyName.ToLower() == normalizedName || x.PolicyCode.ToUpper() == normalizedCode)))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.DuplicatePolicyCode);
		}
	}

	private async Task<CollectionPolicyUpdateModel> BuildValidatedModelAsync(CreateUpdateCollectionPolicyDto input)
	{
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

		var roleIds = model.ApprovalRoleIds.Distinct().ToList();
		if (roleIds.Count > 0 && (await roleRepository.GetListAsync(x => roleIds.Contains(x.Id))).Count != roleIds.Count)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.InvalidWaiverRule).WithData("field", "approvalRoleIds");
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
