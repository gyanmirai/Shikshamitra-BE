using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Accounts.Settings;

[Authorize]
[RemoteService(false)]
public class AccountHeadAppService(
	IRepository<AccountHead, Guid> accountHeadRepository,
	IRepository<TransactionCategory, Guid> categoryRepository,
	IRepository<BankAccount, Guid> bankAccountRepository,
	FinanceScopeAccessService financeScopeAccessService,
	AccountSettingVersionHistoryReader historyReader) : ApplicationService, IAccountHeadAppService
{
	private static readonly IReadOnlySet<string> HistoryFields = new HashSet<string>
	{
		nameof(AccountHead.Name), nameof(AccountHead.HeadType), nameof(AccountHead.ParentAccountHeadId),
		nameof(AccountHead.Description), nameof(AccountHead.IsActive), nameof(AccountHead.DeactivationReason)
	};

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Add)]
	public async Task<AccountHeadResponse> CreateAsync(Guid academicYearId, AccountHeadUpsertRequest input)
	{
		await EnsureScopeAsync(academicYearId);
		await EnsureUniqueAsync(academicYearId, input.HeadName);
		await ValidateParentAsync(academicYearId, input.HeadType, input.ParentAccountHeadId);
		var entity = new AccountHead(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.HeadName, input.HeadType, input.ParentAccountHeadId,
			input.Notes);
		await accountHeadRepository.InsertAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public async Task<PagedResultDto<AccountHeadResponse>> GetListAsync(Guid academicYearId,
		AccountHeadType? headType = null, int page = 1, int pageSize = 10,
		string? search = null, bool includeInactive = true)
	{
		if (page < 1 || pageSize is < 1 or > 100) throw new BusinessException("Finance:Accounts:Settings:InvalidPaging");
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null))
			return new PagedResultDto<AccountHeadResponse>(0, []);
		var query = await accountHeadRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);
		if (headType.HasValue) query = query.Where(x => x.HeadType == headType.Value);
		if (!includeInactive) query = query.Where(x => x.IsActive);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToUpperInvariant();
			query = query.Where(x => x.NormalizedName.Contains(term));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name)
			.Skip((page - 1) * pageSize).Take(pageSize));
		var responses = new List<AccountHeadResponse>();
		foreach (var row in rows) responses.Add(await MapAsync(row));
		return new PagedResultDto<AccountHeadResponse>(total, responses);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public async Task<AccountHeadResponse> GetAsync(Guid id)
	{
		var entity = await accountHeadRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public async Task<List<AccountHeadOptionDto>> GetOptionsAsync(Guid academicYearId, AccountHeadType? headType = null)
	{
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null)) return [];
		var query = await accountHeadRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId && x.IsActive);
		if (headType.HasValue) query = query.Where(x => x.HeadType == headType.Value);
		return await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name).Select(x => new AccountHeadOptionDto
		{
			Id = x.Id, Name = x.Name, HeadType = x.HeadType
		}));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public async Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(
		Guid id, int skipCount = 0, int maxResultCount = 50)
	{
		var entity = await accountHeadRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		return await historyReader.ReadAsync<AccountHead>(id, entity.CreationTime, entity.CreatorId,
			HistoryFields, skipCount, maxResultCount, entity.Version);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Edit)]
	public async Task<AccountHeadResponse> UpdateAsync(Guid id, AccountHeadUpsertRequest input)
	{
		var entity = await accountHeadRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		await EnsureUniqueAsync(entity.AcademicYearId, input.HeadName, id);
		await ValidateParentAsync(entity.AcademicYearId, input.HeadType, input.ParentAccountHeadId, id);
		if (entity.HeadType != input.HeadType && await IsUsedAsync(id))
			throw new BusinessException(AccountSettingErrorCodes.AccountHeadInUse);
		entity.Update(input.HeadName, input.HeadType, input.ParentAccountHeadId,
			input.Notes);
		await accountHeadRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.ChangeStatus)]
	public async Task<AccountHeadResponse> UpdateStatusAsync(Guid id, AccountSettingStatusUpdateRequest input)
	{
		var entity = await accountHeadRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (!input.IsActive && await IsUsedAsync(id)) throw new BusinessException(AccountSettingErrorCodes.AccountHeadInUse);
		if (input.IsActive) entity.Activate(); else entity.Deactivate(input.DeactivationReason ?? string.Empty);
		await accountHeadRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Delete)]
	[UnitOfWork]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await accountHeadRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		if (entity.IsActive) throw new BusinessException(AccountSettingErrorCodes.ActiveRecordCannotBeDeleted);
		if (await IsUsedAsync(id)) throw new BusinessException(AccountSettingErrorCodes.AccountHeadInUse);
		await accountHeadRepository.DeleteAsync(entity, autoSave: true);
	}

	private Task EnsureScopeAsync(Guid academicYearId) =>
		financeScopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);

	private async Task EnsureUniqueAsync(Guid academicYearId, string name, Guid? excludingId = null)
	{
		var normalizedName = AccountSettingConsts.NormalizeName(name);
		if (await accountHeadRepository.AnyAsync(x => x.AcademicYearId == academicYearId &&
			x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.DuplicateName).WithData("Name", name);
	}

	private async Task ValidateParentAsync(Guid academicYearId, AccountHeadType type, Guid? parentId, Guid? entityId = null)
	{
		if (!parentId.HasValue) return;
		if (parentId == entityId) throw new BusinessException(AccountSettingErrorCodes.ParentAccountHeadCycle);
		var parent = await accountHeadRepository.FindAsync(parentId.Value);
		if (parent is null || !parent.IsActive || parent.AcademicYearId != academicYearId || parent.HeadType != type)
			throw new BusinessException(AccountSettingErrorCodes.ParentAccountHeadInvalid);
		var cursor = parent.ParentAccountHeadId;
		while (cursor.HasValue)
		{
			if (cursor == entityId) throw new BusinessException(AccountSettingErrorCodes.ParentAccountHeadCycle);
			cursor = (await accountHeadRepository.FindAsync(cursor.Value))?.ParentAccountHeadId;
		}
	}

	private async Task<bool> IsUsedAsync(Guid id) =>
		await categoryRepository.AnyAsync(x => x.DefaultAccountHeadId == id) ||
		await bankAccountRepository.AnyAsync(x => x.AccountHeadId == id) ||
		await accountHeadRepository.AnyAsync(x => x.ParentAccountHeadId == id);

	private async Task<AccountHeadResponse> MapAsync(AccountHead entity)
	{
		var parent = entity.ParentAccountHeadId.HasValue ? await accountHeadRepository.FindAsync(entity.ParentAccountHeadId.Value) : null;
		return new AccountHeadResponse
		{
			Id = entity.Id, AcademicYearId = entity.AcademicYearId,
			HeadName = entity.Name, HeadType = entity.HeadType,
			Nature = entity.HeadType is AccountHeadType.Asset or AccountHeadType.Expense ? "Debit" : "Credit",
			ParentAccountHeadId = entity.ParentAccountHeadId, ParentAccountHeadName = parent?.Name,
			Notes = entity.Description,
			IsActive = entity.IsActive, DeactivationReason = entity.DeactivationReason,
			Version = entity.Version, ConcurrencyStamp = entity.ConcurrencyStamp,
			IsUsedInDownstream = await IsUsedAsync(entity.Id)
		};
	}

	private static void EnsureConcurrency(AccountHead entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp)) throw new BusinessException(AccountSettingErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, stamp, StringComparison.Ordinal))
			throw new AbpDbConcurrencyException($"Account Head {entity.Id} was modified by another user.");
	}
}
