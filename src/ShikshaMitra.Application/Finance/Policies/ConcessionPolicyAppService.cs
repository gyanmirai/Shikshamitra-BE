using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions;
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
	IRepository<FeeStructure, Guid> feeStructureRepository,
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
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.PolicyName.ToLower().Contains(search) || x.PolicyCode.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.Status == ConcessionPolicyStatus.Active)
			.ThenByDescending(x => x.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));
		var usedIds = await GetUsedIdsAsync(rows.Select(x => x.Id).ToList());
		var items = rows.Select(x =>
		{
			var dto = ConcessionPolicyMapping.ToDto(x);
			dto.IsUsedInDownstream = usedIds.Contains(x.Id);
			return dto;
		}).ToList();
		return new PagedResultDto<ConcessionPolicyDto>(total, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View)]
	public async Task<ConcessionPolicyDto> GetAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		var dto = ConcessionPolicyMapping.ToDto(entity);
		dto.IsUsedInDownstream = (await GetUsedIdsAsync([id])).Contains(id);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add)]
	public async Task<ConcessionPolicyDto> CreateAsync(CreateUpdateConcessionPolicyDto input)
	{
		var code = string.IsNullOrWhiteSpace(input.PolicyCode)
			? $"CON-{GuidGenerator.Create():N}"[..16].ToUpperInvariant()
			: input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, null);
		var entity = new ConcessionPolicy(
			GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			ConcessionPolicyMapping.ToUpdateModel(input, code));
		await repository.InsertAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "created");
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Edit)]
	public async Task<ConcessionPolicyDto> UpdateAsync(Guid id, CreateUpdateConcessionPolicyDto input)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ConcessionPolicyStatus.Archived)
			throw new BusinessException(ConcessionPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (entity.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearCannotChange);
		var targetStatus = ConcessionPolicyMapping.ParseStatus(input.Status);
		if (entity.Status == ConcessionPolicyStatus.Active && targetStatus == ConcessionPolicyStatus.Draft &&
			(await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyInUse);

		var code = string.IsNullOrWhiteSpace(input.PolicyCode) ? entity.PolicyCode : input.PolicyCode.Trim().ToUpperInvariant();
		await EnsureUniqueAsync(input.AcademicYearId, input.PolicyName, code, id);
		entity.Update(ConcessionPolicyMapping.ToUpdateModel(input, code));
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "edited");
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Clone)]
	public async Task<ConcessionPolicyDto> CloneAsync(Guid id, ConcessionPolicyCloneRequestDto input)
	{
		var source = await GetRequiredAsync(id);
		var targetYear = input.TargetAcademicYearId ?? source.AcademicYearId;
		var dto = new CreateUpdateConcessionPolicyDto
		{
			AcademicYearId = targetYear,
			PolicyName = string.IsNullOrWhiteSpace(input.Name) ? $"{source.PolicyName} Copy" : input.Name,
			Status = "Draft",
			Description = source.Description,
			ValueSource = source.ValueSource.ToString(),
			CalculationMethod = source.CalculationMethod.ToString(),
			PercentageValue = source.PercentageValue,
			FixedAmount = source.FixedAmount,
			MaximumPercentage = source.MaximumPercentage,
			MaximumFixedAmount = source.MaximumFixedAmount,
			ApplyOn = source.ApplyOn.ToString(),
			RoundingMethod = source.RoundingMethod.ToString()
		};
		return await CreateAsync(dto);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Archive)]
	public async Task ArchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status == ConcessionPolicyStatus.Archived) return;
		if ((await GetUsedIdsAsync([id])).Contains(id))
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyInUse);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "archived");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Unarchive)]
	public async Task<ConcessionPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await GetRequiredAsync(id);
		if (entity.Status != ConcessionPolicyStatus.Archived) return ConcessionPolicyMapping.ToDto(entity);
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		await WriteVersionAsync(entity, "unarchived");
		return ConcessionPolicyMapping.ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.ViewVersions)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Simulate)]
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
		var normalizedName = name.Trim().ToLower();
		var normalizedCode = code.Trim().ToLower();
		var query = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyName.ToLower() == normalizedName && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(ConcessionPolicyErrorCodes.DuplicateName);
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.AcademicYearId == yearId &&
			x.PolicyCode.ToLower() == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException(ConcessionPolicyErrorCodes.DuplicateCode);
	}

	private async Task<HashSet<Guid>> GetUsedIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0) return [];
		var query = await feeStructureRepository.GetQueryableAsync();
		return (await AsyncExecuter.ToListAsync(query
			.Where(x => x.ConcessionPolicyId.HasValue && ids.Contains(x.ConcessionPolicyId.Value))
			.Select(x => x.ConcessionPolicyId!.Value).Distinct())).ToHashSet();
	}

	private async Task WriteVersionAsync(ConcessionPolicy entity, string action)
	{
		await versionRepository.InsertAsync(new FinancePolicyVersion(
			GuidGenerator.Create(), CurrentTenant.Id, "concession", entity.Id,
			entity.AcademicYearId, entity.Version, entity.PolicyName, entity.Status.ToString(),
			ConcessionPolicyMapping.ToSnapshotJson(entity), action), autoSave: true);
	}

	private static ConcessionPolicyStatus ParseListStatus(string value) => value.Trim().ToLowerInvariant() switch
	{
		"draft" => ConcessionPolicyStatus.Draft,
		"active" => ConcessionPolicyStatus.Active,
		"archived" => ConcessionPolicyStatus.Archived,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidStatus)
	};
}
