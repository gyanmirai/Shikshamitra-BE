using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;
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
public class TransactionCategoryAppService(
	IRepository<TransactionCategory, Guid> categoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	IRepository<Transaction, Guid> transactionRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<ShikshaMitra.Finance.Fees.Charges.ChargeRequest, Guid> chargeRequests,
 ChargeRoutingUsageManager chargeRoutingUsage,
 IRepository<ShikshaMitra.Domain.Finance.Policies.FinancePolicyVersion, Guid> policyVersions,
 IRepository<ShikshaMitra.Finance.Fees.FeeRefund.FeeRefundRequest, Guid> refunds,
 IRepository<ShikshaMitra.Domain.Finance.Policies.CollectionPolicy, Guid> collectionPolicies,
 IRepository<ShikshaMitra.Domain.Finance.Policies.RefundPolicy, Guid> refundPolicies,
 FinanceScopeAccessService financeScopeAccessService,
	AccountSettingVersionHistoryReader historyReader) : ApplicationService, ITransactionCategoryAppService
{
	private static readonly IReadOnlySet<string> HistoryFields = new HashSet<string>
	{
		nameof(TransactionCategory.Code), nameof(TransactionCategory.Name),
		nameof(TransactionCategory.Description), nameof(TransactionCategory.DefaultAccountHeadId),
		nameof(TransactionCategory.EffectiveFrom), nameof(TransactionCategory.EffectiveTo),
		nameof(TransactionCategory.IsActive), nameof(TransactionCategory.DeactivationReason)
	};

	public async Task<List<AccountHeadOptionDto>> GetAccountHeadOptionsAsync(Guid academicYearId, TransactionType categoryType)
	{
		var add = categoryType == TransactionType.Income
			? ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.Add
			: ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.Add;
		var edit = categoryType == TransactionType.Income
			? ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.Edit
			: ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.Edit;
		if (!await AuthorizationService.IsGrantedAsync(add)) await AuthorizationService.CheckAsync(edit);
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null)) return [];
		return [];
	}

	public async Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId,
		CreateTransactionCategoryRequest input, TransactionType categoryType)
	{
		await CheckPermissionAsync(categoryType, "Add");
		await EnsureScopeAsync(academicYearId);
		var id = GuidGenerator.Create();
		var code = id.ToString("N").ToUpperInvariant();
		await EnsureUniqueAsync(academicYearId, categoryType, code, input.Name);
		var entity = new TransactionCategory(id, CurrentTenant.Id, academicYearId,
			categoryType, input.Name, input.Description, input.Tags, true, code);
		await categoryRepository.InsertAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	public async Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId,
		TransactionType categoryType, int page = 1, int pageSize = 10, string? search = null,
		bool includeInactive = true)
	{
		await CheckPermissionAsync(categoryType, "View");
		if (page < 1 || pageSize is < 1 or > 100) throw new BusinessException("Finance:Accounts:Settings:InvalidPaging");
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null))
			return new PaginatedTransactionCategoryResponse { TotalCount = 0, CurrentPage = page, PageSize = pageSize, TotalPages = 0, Items = [] };
		var query = await categoryRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId && x.CategoryType == categoryType);
		if (!includeInactive) query = query.Where(x => x.IsActive);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToUpperInvariant();
			query = query.Where(x => (x.NormalizedCode != null && x.NormalizedCode.Contains(term)) ||
				(x.NormalizedName != null && x.NormalizedName.Contains(term)) || x.Name.ToUpper().Contains(term));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name)
			.Skip((page - 1) * pageSize).Take(pageSize));
		var items = new List<TransactionCategoryResponse>();
		foreach (var row in rows) items.Add(await MapAsync(row));
		return new PaginatedTransactionCategoryResponse
		{
			TotalCount = total, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(total / (double)pageSize), Items = items
		};
	}

	public async Task<TransactionCategoryResponse> GetAsync(Guid id, TransactionType categoryType)
	{
		await CheckPermissionAsync(categoryType, "View");
		var entity = await categoryRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureCategoryType(entity, categoryType);
		return await MapAsync(entity);
	}

	public async Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(
		Guid id, TransactionType categoryType, int skipCount = 0, int maxResultCount = 50)
	{
		await CheckPermissionAsync(categoryType, "View");
		var entity = await categoryRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureCategoryType(entity, categoryType);
		return await historyReader.ReadAsync<TransactionCategory>(id, entity.CreationTime, entity.CreatorId,
			HistoryFields, skipCount, maxResultCount, entity.Version);
	}

	public async Task<TransactionCategoryResponse> UpdateAsync(Guid id,
		UpdateTransactionCategoryRequest input, TransactionType categoryType)
	{
		await CheckPermissionAsync(categoryType, "Edit");
		var entity = await categoryRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureCategoryType(entity, categoryType);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		await EnsureUniqueAsync(entity.AcademicYearId, categoryType, entity.Code ?? id.ToString("N").ToUpperInvariant(), input.Name, id);
		entity.Update(entity.Code ?? id.ToString("N").ToUpperInvariant(), input.Name, input.Description, entity.DefaultAccountHeadId,
			entity.EffectiveFrom, entity.EffectiveTo, input.Tags);
		await categoryRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	public async Task<TransactionCategoryResponse> UpdateStatusAsync(Guid id,
		AccountSettingStatusUpdateRequest input, TransactionType categoryType)
	{
		await CheckPermissionAsync(categoryType, "ChangeStatus");
		var entity = await categoryRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureCategoryType(entity, categoryType);
		EnsureConcurrency(entity, input.ConcurrencyStamp);
		if (!input.IsActive && await IsConfiguredForFuturePaymentsAsync(id))
			throw new BusinessException(AccountSettingErrorCodes.CategoryInUse)
				.WithData("Reason", "Remap active Fee Heads, policies, and open charges before deactivating this category.");
		if (input.IsActive) entity.Activate(); else entity.Deactivate(input.DeactivationReason ?? string.Empty);
		await categoryRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[UnitOfWork]
	public async Task DeleteAsync(Guid id, TransactionType categoryType)
	{
		await CheckPermissionAsync(categoryType, "Delete");
		var entity = await categoryRepository.GetAsync(id);
		await EnsureScopeAsync(entity.AcademicYearId);
		EnsureCategoryType(entity, categoryType);
		if (entity.IsActive) throw new BusinessException(AccountSettingErrorCodes.ActiveRecordCannotBeDeleted);
		if (await IsUsedAsync(id)) throw new BusinessException(AccountSettingErrorCodes.CategoryInUse);
		await categoryRepository.DeleteAsync(entity, autoSave: true);
	}

	private Task EnsureScopeAsync(Guid academicYearId) =>
		financeScopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);

	private async Task EnsureUniqueAsync(Guid academicYearId, TransactionType type, string code,
		string name, Guid? excludingId = null)
	{
		var normalizedCode = AccountSettingConsts.NormalizeCode(code);
		var normalizedName = AccountSettingConsts.NormalizeName(name);
		if (await categoryRepository.AnyAsync(x => x.AcademicYearId == academicYearId && x.CategoryType == type &&
			x.NormalizedCode == normalizedCode && (!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.DuplicateCode).WithData("Code", code);
		if (await categoryRepository.AnyAsync(x => x.AcademicYearId == academicYearId && x.CategoryType == type &&
			((x.NormalizedName != null && x.NormalizedName == normalizedName) || x.Name.ToUpper() == normalizedName) &&
			(!excludingId.HasValue || x.Id != excludingId.Value)))
			throw new BusinessException(AccountSettingErrorCodes.DuplicateName).WithData("Name", name);
	}

	private async Task<bool> IsUsedAsync(Guid id) =>
		await transactionRepository.AnyAsync(x => x.CategoryId == id) ||
		await feeHeadRepository.AnyAsync(x => x.IncomeCategoryId == id) ||
        await chargeRequests.AnyAsync(x => x.IncomeCategoryId == id) ||
        await refunds.AnyAsync(x => x.ExpenseCategoryId == id) ||
        await collectionPolicies.AnyAsync(x => x.LateFeeIncomeCategoryId == id) ||
        await refundPolicies.AnyAsync(x => x.ExpenseCategoryId == id) ||
        await policyVersions.AnyAsync(x=>x.SnapshotJson.Contains(id.ToString()));

 private async Task<bool> IsConfiguredForFuturePaymentsAsync(Guid id)
 {
   if (await feeHeadRepository.AnyAsync(x=>x.IncomeCategoryId == id && x.IsActive) ||
       await collectionPolicies.AnyAsync(x=>x.LateFeeIncomeCategoryId == id && x.Status == ShikshaMitra.Domain.Finance.Policies.CollectionPolicyStatus.Active) ||
       await refundPolicies.AnyAsync(x=>x.ExpenseCategoryId == id && x.Status == ShikshaMitra.Domain.Finance.Policies.RefundPolicyStatus.Active)) return true;
   return await chargeRoutingUsage.HasOpenChargesAsync(categoryId: id);
 }

	private async Task<TransactionCategoryResponse> MapAsync(TransactionCategory entity)
	{
		var account = entity.DefaultAccountHeadId.HasValue ? await accountHeadRepository.FindAsync(entity.DefaultAccountHeadId.Value) : null;
		return new TransactionCategoryResponse
		{
			Id = entity.Id, AcademicYearId = entity.AcademicYearId, Code = entity.Code,
			Name = entity.Name, Description = entity.Description, Tags = entity.Tags.Select(x => x.Value).ToList(),
			DefaultAccountHeadId = entity.DefaultAccountHeadId, DefaultAccountHeadName = account?.Name,
			EffectiveFrom = entity.EffectiveFrom, EffectiveTo = entity.EffectiveTo,
			IsActive = entity.IsActive, DeactivationReason = entity.DeactivationReason,
			Version = entity.Version, ConcurrencyStamp = entity.ConcurrencyStamp,
			IsUsedInDownstream = await IsUsedAsync(entity.Id)
		};
	}

	private async Task CheckPermissionAsync(TransactionType type, string action)
	{
		var root = type == TransactionType.Income ? "Finance.Accounts.Settings.IncomeCategory" : "Finance.Accounts.Settings.ExpenseCategory";
		await AuthorizationService.CheckAsync($"{root}.{action}");
	}

	private static void EnsureCategoryType(TransactionCategory entity, TransactionType expected)
	{
		if (entity.CategoryType.HasValue && entity.CategoryType.Value != expected)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch");
	}

	private static void EnsureConcurrency(TransactionCategory entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp)) throw new BusinessException(AccountSettingErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(entity.ConcurrencyStamp, stamp, StringComparison.Ordinal))
			throw new AbpDbConcurrencyException($"Transaction Category {entity.Id} was modified by another user.");
	}
}
