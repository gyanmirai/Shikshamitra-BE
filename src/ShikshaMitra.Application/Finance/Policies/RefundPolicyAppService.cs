using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
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
public class RefundPolicyAppService(
	IRepository<RefundPolicy, Guid> repository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<TransactionCategory, Guid> expenseCategoryRepository,
	IRepository<BankAccount, Guid> bankAccountRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
	FinanceRoutingManager routingManager,
	FinanceScopeAccessService scopeAccess,
	RefundPolicyCalculationService calculationService)
	: ApplicationService, IRefundPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<PagedResultDto<RefundPolicyDto>> GetListAsync(RefundPolicyListRequestDto input)
	{
		var query = (await repository.GetQueryableAsync()).Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			var status = ParseListStatus(input.Status);
			query = query.Where(x => x.Status == status);
		}
		else
		{
			query = query.Where(x => x.Status != RefundPolicyStatus.Archived);
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
		if (Enum.TryParse<RefundAmountMode>(input.AmountMode, true, out var amountMode) && Enum.IsDefined(amountMode))
			query = query.Where(x => x.AmountMode == amountMode);
		if (Enum.TryParse<RefundFeeHeadScope>(input.FeeHeadScope, true, out var feeHeadScope) && Enum.IsDefined(feeHeadScope))
			query = query.Where(x => x.FeeHeadScope == feeHeadScope);
		if (Enum.TryParse<RefundPayoutMethod>(input.AllowedPayoutMethod, true, out var payoutMethod) && Enum.IsDefined(payoutMethod))
		{
			// RefundPolicy persists payout enums as JSON numbers.
			var method = ((int)payoutMethod).ToString();
			var only = $"[{method}]";
			var first = $"[{method},";
			var middle = $",{method},";
			var last = $",{method}]";
			query = query.Where(x => x.AllowedPayoutMethodsJson == only ||
				x.AllowedPayoutMethodsJson.StartsWith(first) ||
				x.AllowedPayoutMethodsJson.Contains(middle) ||
				x.AllowedPayoutMethodsJson.EndsWith(last));
		}
		if (input.AllowPartialRefund.HasValue)
			query = query.Where(x => x.AllowPartialRefund == input.AllowPartialRefund.Value);
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
		// Payout methods and selected fee-head counts are stored as JSON. Sort the
		// year-scoped result before paging so these displayed values sort globally.
		var candidates = (await AsyncExecuter.ToListAsync(query)).Select(RefundPolicyMapping.ToDto).ToList();
		var sortParts = input.Sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
		var sortColumn = sortParts.Length > 0 ? sortParts[0].ToLowerInvariant() : string.Empty;
		var descending = sortParts.Length > 1 && sortParts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		IOrderedEnumerable<RefundPolicyDto> sorted = sortColumn switch
		{
			"policyname" => descending ? candidates.OrderByDescending(x => x.PolicyName) : candidates.OrderBy(x => x.PolicyName),
			"amountmode" => descending
				? candidates.OrderByDescending(x => x.AmountMode == "FixedAmount" ? 0 :
					x.AmountMode == "RemainingPaidAmount" ? 1 : x.AmountMode == "Percentage" ? 2 : 3)
				: candidates.OrderBy(x => x.AmountMode == "FixedAmount" ? 0 :
					x.AmountMode == "RemainingPaidAmount" ? 1 : x.AmountMode == "Percentage" ? 2 : 3),
			"feeheadscope" => descending
				? candidates.OrderByDescending(x => x.FeeHeadScope).ThenByDescending(x => x.ApplicableFeeHeadIds.Count)
				: candidates.OrderBy(x => x.FeeHeadScope).ThenBy(x => x.ApplicableFeeHeadIds.Count),
			"allowedpayoutmethods" => descending
				? candidates.OrderByDescending(x => x.AllowedPayoutMethods.Count)
				: candidates.OrderBy(x => x.AllowedPayoutMethods.Count),
			"allowpartialrefund" => descending ? candidates.OrderBy(x => x.AllowPartialRefund) : candidates.OrderByDescending(x => x.AllowPartialRefund),
			"updatedat" => descending ? candidates.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime) : candidates.OrderBy(x => x.LastModificationTime ?? x.CreationTime),
			"status" => descending ? candidates.OrderByDescending(x => x.Status) : candidates.OrderBy(x => x.Status),
			_ => candidates.OrderByDescending(x => x.IsDefault)
				.ThenByDescending(x => x.Status == "Active")
				.ThenByDescending(x => x.CreationTime)
		};
		var items = sorted.ThenBy(x => x.Id)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount)
			.ToList();
		var usedIds = await GetUsedIdsAsync(items.Select(x => x.Id).ToList());
		foreach (var item in items)
			item.IsUsedInDownstream = usedIds.Contains(item.Id);
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "refund", items);
		return new PagedResultDto<RefundPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<RefundPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		var dto = RefundPolicyMapping.ToDto(entity);
		await PopulateDisplayNamesAsync(dto);
		dto.IsUsedInDownstream = (await GetUsedIdsAsync([id])).Contains(id);
		await CurrentPolicyVersionLookup.PopulateAsync(versionRepository, "refund", new[] { dto });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<RefundPolicyImpactDto> GetImpactAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		return new RefundPolicyImpactDto
		{
			PolicyId = entity.Id,
			PolicyName = entity.PolicyName,
			Status = entity.Status.ToString(),
			CurrentVersion = entity.Version,
			AttachedFeeStructureCount = await feeStructureRepository.CountAsync(x => x.RefundPolicyId == id),
			StudentAssignmentCount = await studentFeeStructureRepository.CountAsync(x => x.RefundPolicyId == id),
			ChangeReasonRequired = entity.Status == RefundPolicyStatus.Active
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add)]
	public async Task<RefundPolicyDto> CreateAsync(CreateUpdateRefundPolicyDto input)
	{
		await EnsureReferencesAsync(input);
		var code = string.IsNullOrWhiteSpace(input.PolicyCode)
			? $"REF-{GuidGenerator.Create():N}"[..16].ToUpperInvariant()
			: input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, null);
		var model = RefundPolicyMapping.ToUpdateModel(input, code);
		if (model.Status == RefundPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, null, input.PolicyName, input.ChangeReason);
		var entity = new RefundPolicy(
			GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			model);
		await repository.InsertAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "created");
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add)]
	public async Task<RefundPolicyDto> UpdateAsync(Guid id, CreateUpdateRefundPolicyDto input)
	{
		var entity = await GetRequiredAsync(id);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (entity.Status == RefundPolicyStatus.Archived)
			throw new BusinessException(RefundPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (entity.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearCannotChange);
		var targetStatus = RefundPolicyMapping.ParseStatus(input.Status);
		if (entity.Status == RefundPolicyStatus.Active && targetStatus == RefundPolicyStatus.Draft &&
			(await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(RefundPolicyErrorCodes.PolicyInUse);
		if (entity.Status == RefundPolicyStatus.Active && string.IsNullOrWhiteSpace(input.ChangeReason))
			throw new BusinessException(RefundPolicyErrorCodes.ChangeReasonRequired);

		await EnsureReferencesAsync(input);
		var code = string.IsNullOrWhiteSpace(input.PolicyCode) ? entity.PolicyCode : input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, id);
		if (entity.IsDefault && (!input.IsDefault || targetStatus != RefundPolicyStatus.Active))
			throw new BusinessException(RefundPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		var model = RefundPolicyMapping.ToUpdateModel(input, code);
		if (model.Status == RefundPolicyStatus.Active &&
			!await HasActiveDefaultAsync(input.AcademicYearId))
			model = model with { IsDefault = true };
		if (model.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);
		entity.Update(model);
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add)]
	public async Task<RefundPolicyDto> CloneAsync(Guid id, RefundPolicyCloneRequestDto input)
	{
		var source = await GetRequiredAsync(id);
		var targetYear = input.TargetAcademicYearId ?? source.AcademicYearId;
		var sameYear = targetYear == source.AcademicYearId;
		return await CreateAsync(new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = targetYear,
			PolicyName = string.IsNullOrWhiteSpace(input.Name) ? $"{source.PolicyName} Copy" : input.Name,
			Status = "Draft",
			IsDefault = false,
			Description = source.Description,
			FeeHeadScope = sameYear ? source.FeeHeadScope.ToString() : RefundFeeHeadScope.AllFeeHeads.ToString(),
			ApplicableFeeHeadIds = sameYear ? [.. source.GetApplicableFeeHeadIds()] : [],
			IncludePaidCharges = source.IncludePaidCharges,
			IncludePaidLateFee = source.IncludePaidLateFee,
			AllowPartialRefund = source.AllowPartialRefund,
			AmountMode = source.AmountMode.ToString(),
			PercentageValue = source.PercentageValue,
			FixedAmount = source.FixedAmount,
			MaximumPerRequest = source.MaximumPerRequest,
			MaximumPerStudentYear = source.MaximumPerStudentYear,
			RoundingMethod = source.RoundingMethod.ToString(),
			DeductionType = source.DeductionType.ToString(),
			DeductionValue = source.DeductionValue,
			MaximumDeduction = source.MaximumDeduction,
			DeductionExplanation = source.DeductionExplanation,
			AllowedPayoutMethods = source.GetAllowedPayoutMethods().Select(x => x.ToString()).ToList(),
			ExpenseCategoryId = sameYear ? source.ExpenseCategoryId : null,
            DefaultPayingBankAccountId = sameYear ? source.DefaultPayingBankAccountId : null,
            ProcessingTargetDays = source.ProcessingTargetDays
		});
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Delete)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == RefundPolicyStatus.Archived) return;
		if (entity.IsDefault)
			throw new BusinessException(RefundPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if ((await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(RefundPolicyErrorCodes.PolicyInUse);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add)]
	public async Task<RefundPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status != RefundPolicyStatus.Archived) return RefundPolicyMapping.ToDto(entity);
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "unarchived");
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(
		Guid id, FinancePolicyVersionListRequestDto input)
	{
		await GetRequiredAsync(id);
		var query = (await versionRepository.GetQueryableAsync())
			.Where(x => x.PolicyType == "refund" && x.PolicyId == id);
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<RefundPolicySimulateResponseDto> SimulateAsync(Guid id, RefundPolicySimulateRequestDto input)
	{
		var entity = await GetRequiredAsync(id);
		var result = calculationService.Calculate(entity.ToSnapshot(), new RefundPolicyCalculationInput(
			input.SettledPaidAmount,
			input.PriorProcessedRefund,
			input.ActiveReservation,
			input.StudentYearRefundedOrReserved,
			input.RequestedAmount));
		return new RefundPolicySimulateResponseDto
		{
			SettledPaidAmount = result.SettledPaidAmount,
			PriorProcessedRefund = result.PriorProcessedRefund,
			ActiveReservation = result.ActiveReservation,
			RemainingEligible = result.RemainingEligible,
			RawRefund = result.RawRefund,
			DeductionAmount = result.DeductionAmount,
			CapAdjustment = result.CapAdjustment,
			FinalRefund = result.FinalRefund,
			Explanation = $"{entity.AmountMode}; {entity.DeductionType} deduction; {entity.RoundingMethod} rounding."
		};
	}

	private async Task EnsureReferencesAsync(CreateUpdateRefundPolicyDto input)
	{
		await scopeAccess.EnsureAcademicContextAccessibleAsync(input.AcademicYearId, null, null, null, null);
		var active = input.Status == "Active";
		if (active || input.ExpenseCategoryId.HasValue)
			await routingManager.RequireCategoryAsync(input.AcademicYearId, input.ExpenseCategoryId, TransactionType.Expense);
		if (input.DefaultPayingBankAccountId.HasValue || (active && input.AllowedPayoutMethods.Any(x => x != "Cash")))
			await routingManager.RequireBankAsync(input.AcademicYearId, input.DefaultPayingBankAccountId);
		if (await academicYearRepository.FindAsync(input.AcademicYearId) == null)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearRequired);
		if (!string.Equals(input.FeeHeadScope, RefundFeeHeadScope.SelectedFeeHeads.ToString(), StringComparison.OrdinalIgnoreCase))
			return;
		var ids = input.ApplicableFeeHeadIds.Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id));
		if (ids.Count == 0 || heads.Count != ids.Count || heads.Any(x => !x.IsActive || x.AcademicYearId != input.AcademicYearId))
			throw new BusinessException(RefundPolicyErrorCodes.InvalidFeeHeadReference);
	}

	private async Task<RefundPolicy> GetRequiredAsync(Guid id) =>
		await repository.FindAsync(id) ??
		throw new BusinessException(RefundPolicyErrorCodes.PolicyNotFound).WithData("id", id);

	private async Task EnsureUniqueAsync(Guid yearId, string name, string code, Guid? excludeId)
	{
		var normalizedName = name.Trim().ToLower();
		var normalizedCode = code.Trim().ToLower();
		var query = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyName.ToLower() == normalizedName && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(RefundPolicyErrorCodes.DuplicateName);
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyCode.ToLower() == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(RefundPolicyErrorCodes.DuplicateCode);
	}

	private async Task<HashSet<Guid>> GetUsedIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0) return [];
		var feeStructureQuery = await feeStructureRepository.GetQueryableAsync();
		var assignmentQuery = await studentFeeStructureRepository.GetQueryableAsync();
		var feeStructurePolicyIds = await AsyncExecuter.ToListAsync(feeStructureQuery
			.Where(x => x.RefundPolicyId.HasValue && ids.Contains(x.RefundPolicyId.Value))
			.Select(x => x.RefundPolicyId!.Value).Distinct());
		var assignmentPolicyIds = await AsyncExecuter.ToListAsync(assignmentQuery
			.Where(x => x.RefundPolicyId.HasValue && ids.Contains(x.RefundPolicyId.Value))
			.Select(x => x.RefundPolicyId!.Value).Distinct());
		return feeStructurePolicyIds.Concat(assignmentPolicyIds).ToHashSet();
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
			x.Status == RefundPolicyStatus.Active && x.IsDefault);

	private async Task WriteVersionAsync(RefundPolicy entity, string action, string? reason = null)
	{
		var snapshot = entity.ToSnapshot();
		var previousVersion = (await versionRepository.GetListAsync(x => x.PolicyType == "refund" && x.PolicyId == entity.Id))
			.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
		RefundPolicySnapshot? previous = null;
		if (previousVersion != null)
		{
			try { previous = JsonSerializer.Deserialize<RefundPolicySnapshot>(previousVersion.SnapshotJson); }
			catch (JsonException) { /* Legacy audit data must not prevent a policy write. */ }
		}
		var ids = snapshot.ApplicableFeeHeadIds.Distinct().ToList();
		var heads = ids.Count == 0 ? [] : await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id));
		var names = heads.ToDictionary(x => x.Id, x => x.Name);
		foreach (var id in ids)
			if (!names.ContainsKey(id) && previous?.FeeHeadNames?.TryGetValue(id, out var oldName) == true)
				names[id] = oldName;
		var category = entity.ExpenseCategoryId.HasValue
			? await expenseCategoryRepository.FindAsync(entity.ExpenseCategoryId.Value) : null;
		var bank = entity.DefaultPayingBankAccountId.HasValue
			? await bankAccountRepository.FindAsync(entity.DefaultPayingBankAccountId.Value) : null;
		snapshot = snapshot with {
			FeeHeadNames = names,
			ExpenseCategoryName = category?.Name ?? (entity.ExpenseCategoryId == previous?.ExpenseCategoryId ? previous?.ExpenseCategoryName : null),
			DefaultPayingBankAccountName = bank == null
				? (entity.DefaultPayingBankAccountId == previous?.DefaultPayingBankAccountId ? previous?.DefaultPayingBankAccountName : null)
				: $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}"
		};
		await versionRepository.InsertAsync(new FinancePolicyVersion(
			GuidGenerator.Create(), CurrentTenant.Id, "refund", entity.Id,
			entity.AcademicYearId, entity.Version, entity.PolicyName, entity.Status.ToString(),
			JsonSerializer.Serialize(snapshot), action, reason), autoSave: true);
	}

	private async Task PopulateDisplayNamesAsync(RefundPolicyDto dto)
	{
		var ids = dto.ApplicableFeeHeadIds.Distinct().ToList();
		var heads = ids.Count == 0 ? [] : await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id));
		foreach (var head in heads)
		{
			dto.FeeHeadNames[head.Id] = head.Name;
			dto.FeeHeadStatuses[head.Id] = head.IsActive ? "Active" : "Inactive";
		}
		var latest = (await versionRepository.GetListAsync(x => x.PolicyType == "refund" && x.PolicyId == dto.Id))
			.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
		RefundPolicySnapshot? recorded = null;
		if (latest != null)
		{
			try
			{
				recorded = JsonSerializer.Deserialize<RefundPolicySnapshot>(latest.SnapshotJson);
				foreach (var id in ids.Where(id => !dto.FeeHeadNames.ContainsKey(id)))
					if (recorded?.FeeHeadNames?.TryGetValue(id, out var name) == true)
					{
						dto.FeeHeadNames[id] = name;
						dto.FeeHeadStatuses[id] = "Deleted";
					}
			}
			catch (JsonException) { /* Preserve the policy response for legacy snapshots. */ }
		}
		var category = dto.ExpenseCategoryId.HasValue
			? await expenseCategoryRepository.FindAsync(dto.ExpenseCategoryId.Value) : null;
		var bank = dto.DefaultPayingBankAccountId.HasValue
			? await bankAccountRepository.FindAsync(dto.DefaultPayingBankAccountId.Value) : null;
		dto.ExpenseCategoryName = category?.Name ?? (dto.ExpenseCategoryId == recorded?.ExpenseCategoryId ? recorded?.ExpenseCategoryName : null);
		dto.DefaultPayingBankAccountName = bank == null
			? (dto.DefaultPayingBankAccountId == recorded?.DefaultPayingBankAccountId ? recorded?.DefaultPayingBankAccountName : null)
			: $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}";
	}

	private static void EnsureConcurrency(RefundPolicy entity, string? concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp))
			throw new BusinessException(RefundPolicyErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(RefundPolicyErrorCodes.ConcurrencyConflict);
	}

	private static RefundPolicyStatus ParseListStatus(string value) => value.Trim().ToLowerInvariant() switch
	{
		"draft" => RefundPolicyStatus.Draft,
		"active" => RefundPolicyStatus.Active,
		"archived" => RefundPolicyStatus.Archived,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidStatus)
	};
}
