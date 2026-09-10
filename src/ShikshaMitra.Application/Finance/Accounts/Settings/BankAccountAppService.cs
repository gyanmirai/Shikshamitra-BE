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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Accounts.Settings;

[Authorize]
[RemoteService(false)]
public class BankAccountAppService(
	IRepository<BankAccount, Guid> bankAccountRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	FinanceScopeAccessService financeScopeAccessService,
	AccountSettingVersionHistoryReader historyReader) : ApplicationService, IBankAccountAppService
{
	private static readonly IReadOnlySet<string> HistoryFields = new HashSet<string>
	{
		nameof(BankAccount.Code), nameof(BankAccount.AccountHeadId), nameof(BankAccount.BankName),
		nameof(BankAccount.BranchName), nameof(BankAccount.BranchAddress),
		nameof(BankAccount.AccountHolderName), nameof(BankAccount.AccountType),
		nameof(BankAccount.Purpose), nameof(BankAccount.EffectiveFrom), nameof(BankAccount.EffectiveTo),
		nameof(BankAccount.IsActive), nameof(BankAccount.DeactivationReason)
	};

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Add)]
	public async Task<BankAccountResponse> CreateAsync(Guid academicYearId, CreateBankAccountRequest input)
	{
		await EnsureScopeAsync(academicYearId);
		await ValidateAccountHeadAsync(academicYearId, input.AccountHeadId);
		await EnsureUniqueAsync(academicYearId, input.Code, input.AccountHeadId, input.AccountNumber);
		var entity = new BankAccount(GuidGenerator.Create(), academicYearId, input.BankName,
			input.BranchName, input.BranchAddress, input.AccountHolderName, input.AccountType,
			input.AccountNumber, input.Purpose, input.IfscCode, input.UpiId, true, CurrentTenant.Id,
			input.Code, input.AccountHeadId, input.EffectiveFrom, input.EffectiveTo);
		await bankAccountRepository.InsertAsync(entity, autoSave: true);
		return await MapAsync(entity, includeSensitive: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public async Task<PaginatedBankAccountResponse> GetListAsync(Guid academicYearId, int page = 1,
		int pageSize = 10, string? search = null, bool includeInactive = true)
	{
		if (page < 1 || pageSize is < 1 or > 100) throw new BusinessException("Finance:Accounts:Settings:InvalidPaging");
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null))
			return new PaginatedBankAccountResponse { TotalCount = 0, CurrentPage = page, PageSize = pageSize, TotalPages = 0, Items = [] };
		var query = await bankAccountRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);
		if (!includeInactive) query = query.Where(x => x.IsActive);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToUpperInvariant();
			query = query.Where(x => (x.NormalizedCode != null && x.NormalizedCode.Contains(term)) ||
				x.BankName.ToUpper().Contains(term) || x.BranchName.ToUpper().Contains(term) ||
				x.AccountHolderName.ToUpper().Contains(term));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.BankName)
			.Skip((page - 1) * pageSize).Take(pageSize));
		var items = new List<BankAccountResponse>();
		foreach (var row in rows) items.Add(await MapAsync(row, includeSensitive: false));
		return new PaginatedBankAccountResponse
		{
			TotalCount = total, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(total / (double)pageSize), Items = items
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public async Task<BankAccountResponse> GetAsync(Guid id)
	{
		var entity = await bankAccountRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		return await MapAsync(entity, includeSensitive: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public async Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(
		Guid id, int skipCount = 0, int maxResultCount = 50)
	{
		var entity = await bankAccountRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		return await historyReader.ReadAsync<BankAccount>(id, entity.CreationTime, entity.CreatorId,
			HistoryFields, skipCount, maxResultCount);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Edit)]
	public async Task<BankAccountResponse> UpdateAsync(Guid id, UpdateBankAccountRequest input)
	{
		var entity = await bankAccountRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		await ValidateAccountHeadAsync(entity.AcademicYearId, input.AccountHeadId);
		await EnsureUniqueAsync(entity.AcademicYearId, input.Code, input.AccountHeadId, input.AccountNumber, id);
		entity.Update(input.Code, input.AccountHeadId, input.BankName, input.BranchName,
			input.BranchAddress, input.AccountHolderName, input.AccountType, input.AccountNumber,
			input.Purpose, input.IfscCode, input.UpiId, input.EffectiveFrom, input.EffectiveTo);
		await bankAccountRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity, includeSensitive: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.ChangeStatus)]
	public async Task<BankAccountResponse> UpdateStatusAsync(Guid id, AccountSettingStatusUpdateRequest input)
	{
		var entity = await bankAccountRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (input.IsActive) entity.Activate(); else entity.Deactivate(input.DeactivationReason ?? string.Empty);
		await bankAccountRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity, includeSensitive: false);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Delete)]
	[UnitOfWork]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await bankAccountRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		if (entity.IsActive) throw new BusinessException(AccountSettingErrorCodes.ActiveRecordCannotBeDeleted);
		await bankAccountRepository.DeleteAsync(entity, autoSave: true);
	}

	private Task EnsureScopeAsync(Guid academicYearId) =>
		financeScopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);

	private async Task ValidateAccountHeadAsync(Guid academicYearId, Guid? accountHeadId)
	{
		if (!accountHeadId.HasValue)
			throw new BusinessException(AccountSettingErrorCodes.InvalidAccountHeadType);
		var head = await accountHeadRepository.FindAsync(accountHeadId.Value);
		if (head is null || head.AcademicYearId != academicYearId || head.HeadType != AccountHeadType.Asset)
			throw new BusinessException(AccountSettingErrorCodes.InvalidAccountHeadType).WithData("Expected", AccountHeadType.Asset);
		if (!head.IsActive) throw new BusinessException(AccountSettingErrorCodes.AccountHeadMustBeActive);
	}

	private async Task EnsureUniqueAsync(Guid academicYearId, string code, Guid? accountHeadId,
		string accountNumber, Guid? excludingId = null)
	{
		var normalizedCode = AccountSettingConsts.NormalizeCode(code);
		if (await bankAccountRepository.AnyAsync(x => x.AcademicYearId == academicYearId &&
			x.NormalizedCode == normalizedCode && (!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.DuplicateCode).WithData("Code", code);
		if (accountHeadId.HasValue && await bankAccountRepository.AnyAsync(x => x.AcademicYearId == academicYearId &&
			x.AccountHeadId == accountHeadId && (!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.BankAccountHeadAlreadyAssigned);
		if (await bankAccountRepository.AnyAsync(x => x.AcademicYearId == academicYearId &&
			x.AccountNumber == accountNumber && (!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.DuplicateName).WithData("Field", "AccountNumber");
	}

	private async Task<BankAccountResponse> MapAsync(BankAccount entity, bool includeSensitive)
	{
		var head = entity.AccountHeadId.HasValue ? await accountHeadRepository.FindAsync(entity.AccountHeadId.Value) : null;
		return new BankAccountResponse
		{
			Id = entity.Id, AcademicYearId = entity.AcademicYearId, Code = entity.Code,
			AccountHeadId = entity.AccountHeadId, AccountHeadName = head?.Name,
			BankName = entity.BankName, BranchName = entity.BranchName, BranchAddress = entity.BranchAddress,
			AccountHolderName = entity.AccountHolderName, AccountType = entity.AccountType,
			AccountNumber = includeSensitive ? entity.AccountNumber : null,
			MaskedAccountNumber = Mask(entity.AccountNumber), Purpose = entity.Purpose,
			IfscCode = includeSensitive ? entity.IfscCode : MaskIfsc(entity.IfscCode),
			UpiId = includeSensitive ? entity.UpiId : null,
			EffectiveFrom = entity.EffectiveFrom, EffectiveTo = entity.EffectiveTo,
			IsActive = entity.IsActive, DeactivationReason = entity.DeactivationReason,
			Version = entity.Version, ConcurrencyStamp = entity.ConcurrencyStamp,
			IsUsedInDownstream = false
		};
	}

	private static string Mask(string value) => value.Length <= 4 ? value : new string('*', value.Length - 4) + value[^4..];
	private static string MaskIfsc(string value) => value.Length <= 4 ? value : value[..4] + new string('*', value.Length - 4);

	private static void EnsureConcurrency(BankAccount entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp)) throw new BusinessException(AccountSettingErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, stamp, StringComparison.Ordinal))
			throw new AbpDbConcurrencyException($"Bank Account {entity.Id} was modified by another user.");
	}
}
