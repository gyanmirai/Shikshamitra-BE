using System;
using System.Collections.Generic;
using System.Linq;
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
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
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
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.PolicyName.ToLower().Contains(search) || x.PolicyCode.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsDefault)
			.ThenByDescending(x => x.Status == RefundPolicyStatus.Active)
			.ThenByDescending(x => x.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));
		var usedIds = await GetUsedIdsAsync(rows.Select(x => x.Id).ToList());
		var items = rows.Select(x =>
		{
			var dto = RefundPolicyMapping.ToDto(x);
			dto.IsUsedInDownstream = usedIds.Contains(x.Id);
			return dto;
		}).ToList();
		return new PagedResultDto<RefundPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View)]
	public async Task<RefundPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		var dto = RefundPolicyMapping.ToDto(entity);
		dto.IsUsedInDownstream = (await GetUsedIdsAsync([id])).Contains(id);
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
		if (input.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, null, input.PolicyName, input.ChangeReason);
		var entity = new RefundPolicy(
			GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			RefundPolicyMapping.ToUpdateModel(input, code));
		await repository.InsertAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "created");
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Edit)]
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
		if (input.IsDefault)
			await ReplaceExistingDefaultAsync(input.AcademicYearId, id, input.PolicyName, input.ChangeReason);
		entity.Update(RefundPolicyMapping.ToUpdateModel(input, code));
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "edited", input.ChangeReason);
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Clone)]
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
			EligiblePaymentState = source.EligiblePaymentState.ToString(),
			FeeHeadScope = sameYear ? source.FeeHeadScope.ToString() : RefundFeeHeadScope.AllFeeHeads.ToString(),
			ApplicableFeeHeadIds = sameYear ? [.. source.GetApplicableFeeHeadIds()] : [],
			IncludePaidCharges = source.IncludePaidCharges,
			IncludePaidLateFee = source.IncludePaidLateFee,
			RequestDeadlineDays = source.RequestDeadlineDays,
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
			ProcessingTargetDays = source.ProcessingTargetDays
		});
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Archive)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == RefundPolicyStatus.Archived) return;
		if ((await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(RefundPolicyErrorCodes.PolicyInUse);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Unarchive)]
	public async Task<RefundPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status != RefundPolicyStatus.Archived) return RefundPolicyMapping.ToDto(entity);
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "unarchived");
		return RefundPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.ViewVersions)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Simulate)]
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

	private async Task WriteVersionAsync(RefundPolicy entity, string action, string? reason = null)
	{
		await versionRepository.InsertAsync(new FinancePolicyVersion(
			GuidGenerator.Create(), CurrentTenant.Id, "refund", entity.Id,
			entity.AcademicYearId, entity.Version, entity.PolicyName, entity.Status.ToString(),
			RefundPolicyMapping.ToSnapshotJson(entity), action, reason), autoSave: true);
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
