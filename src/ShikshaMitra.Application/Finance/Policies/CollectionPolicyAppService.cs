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

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class CollectionPolicyAppService(
	IRepository<CollectionPolicy, Guid> repository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<StudentFeePolicyOverride, Guid> overrideRepository,
	IRepository<IdentityUser, Guid> userRepository,
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

		return new PagedResultDto<CollectionPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<CollectionPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetEntityOrThrowAsync(id);
		var dto = CollectionPolicyMapping.ToDto(entity);
		dto.IsUsedInDownstream = (await GetUsageCountsAsync(new[] { id })).Contains(id);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	public async Task<CollectionPolicyDto> CreateAsync(CreateUpdateCollectionPolicyDto input)
	{
		var model = CollectionPolicyMapping.ToUpdateModel(input);
		if (model.IsDefault)
		{
			await EnsureNoOtherDefaultAsync(input.AcademicYearId, excludeId: null);
		}

		var entity = new CollectionPolicy(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, model);
		await repository.InsertAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, dto, "created");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Edit)]
	public async Task<CollectionPolicyDto> UpdateAsync(Guid id, CreateUpdateCollectionPolicyDto input)
	{
		var entity = await GetEntityOrThrowAsync(id);

		if (entity.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearCannotChange);
		}

		var model = CollectionPolicyMapping.ToUpdateModel(input);
		if (model.IsDefault)
		{
			await EnsureNoOtherDefaultAsync(input.AcademicYearId, excludeId: id);
		}

		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, dto, "edited");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	public async Task<CollectionPolicyDto> CloneAsync(Guid id, CollectionPolicyCloneRequestDto input)
	{
		var source = await GetEntityOrThrowAsync(id);
		var sourceDto = CollectionPolicyMapping.ToDto(source);
		var targetAcademicYearId = input.TargetAcademicYearId ?? source.AcademicYearId;

		var createDto = new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = targetAcademicYearId,
			PolicyName = string.IsNullOrWhiteSpace(input.Name) ? $"{source.PolicyName} Copy" : input.Name!,
			PolicyCode = $"CP-{GuidGenerator.Create().ToString("N")[..8].ToUpperInvariant()}",
			Description = source.Description,
			Status = CollectionPolicyMapping.MapStatus(source.Status),
			IsDefault = false,
			EffectiveFrom = source.EffectiveFrom,
			EffectiveTo = source.EffectiveTo,
			EnableLateFee = sourceDto.EnableLateFee,
			GracePeriodDays = sourceDto.GracePeriodDays,
			GracePeriodType = sourceDto.GracePeriodType,
			LateFeeApplyOnFeeHeads = sourceDto.LateFeeApplyOnFeeHeads,
			LateFeeType = sourceDto.LateFeeType,
			FlatAmount = sourceDto.FlatAmount,
			Percentage = sourceDto.Percentage,
			PercentageBase = sourceDto.PercentageBase,
			AmountPerDay = sourceDto.AmountPerDay,
			AmountPerWeek = sourceDto.AmountPerWeek,
			AmountPerMonth = sourceDto.AmountPerMonth,
			CompoundFrequency = sourceDto.CompoundFrequency,
			EscalationRules = sourceDto.EscalationRules,
			RoundingMethod = sourceDto.RoundingMethod,
			ConsiderAsIncome = sourceDto.ConsiderAsIncome,
			LateFeeIncomeHeadId = sourceDto.LateFeeIncomeHeadId,
			MaximumLateFeeAmount = sourceDto.MaximumLateFeeAmount,
			MaximumChargingDays = sourceDto.MaximumChargingDays,
			EnableWaiver = sourceDto.EnableWaiver,
			WaiverApplyOnFeeHeads = sourceDto.WaiverApplyOnFeeHeads,
			MaximumWaiverAmount = sourceDto.MaximumWaiverAmount,
			MaximumWaiverPercentage = sourceDto.MaximumWaiverPercentage,
			WaiverExpiry = sourceDto.WaiverExpiry,
			RequiresApproval = sourceDto.RequiresApproval,
			ApprovalRoleIds = sourceDto.ApprovalRoleIds,
			ApprovalThresholdAmount = sourceDto.ApprovalThresholdAmount,
			AllowOnlineCollection = sourceDto.AllowOnlineCollection
		};

		var model = CollectionPolicyMapping.ToUpdateModel(createDto);
		var entity = new CollectionPolicy(GuidGenerator.Create(), CurrentTenant.Id, targetAcademicYearId, model);
		await repository.InsertAsync(entity, autoSave: true);

		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, dto, "cloned");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Archive)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetEntityOrThrowAsync(id);
		if (entity.Status == CollectionPolicyStatus.Archived)
		{
			return;
		}

		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, CollectionPolicyMapping.ToDto(entity), "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Archive)]
	public async Task<CollectionPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetEntityOrThrowAsync(id);
		if (entity.Status != CollectionPolicyStatus.Archived)
		{
			return CollectionPolicyMapping.ToDto(entity);
		}

		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = CollectionPolicyMapping.ToDto(entity);
		await WriteVersionAsync(entity, dto, "unarchived");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, FinancePolicyVersionListRequestDto input)
	{
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
				Clock.Now));

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
	/// Enforces "at most one isDefault Collection Policy per academic year" — the server-side
	/// authority behind the frontend's client-side check in useCollectionPolicyForm.ts.
	/// </summary>
	private async Task EnsureNoOtherDefaultAsync(Guid academicYearId, Guid? excludeId)
	{
		var query = await repository.GetQueryableAsync();
		var conflict = query.FirstOrDefault(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == CollectionPolicyStatus.Active &&
			(excludeId == null || x.Id != excludeId));

		if (conflict is not null)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.DuplicateDefaultForAcademicYear)
				.WithData("conflictingPolicyName", conflict.PolicyName);
		}
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

	private async Task WriteVersionAsync(CollectionPolicy entity, CollectionPolicyDto dto, string changeAction)
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
			CollectionPolicyMapping.ToSnapshotJson(dto),
			changeAction);

		await versionRepository.InsertAsync(version, autoSave: true);
	}
}
