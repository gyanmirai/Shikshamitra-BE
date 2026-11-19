using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Uow;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public partial class FeeHeadAppService : ApplicationService, IFeeHeadAppService
{
	private readonly IRepository<FeeHead, Guid> feeHeadRepository;
	private readonly IRepository<FeeCategory, Guid> feeCategoryRepository;
	private readonly IRepository<TransactionCategory, Guid> incomeCategoryRepository;
	private readonly IRepository<AccountHead, Guid> accountHeadRepository;
	private readonly FeeCategoryManager feeCategoryManager;
	private readonly FeeHeadAccountingManager accountingManager;
	private readonly FeeHeadDependencyQuery dependencyQuery;

	public FeeHeadAppService(
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeCategory, Guid> feeCategoryRepository,
		IRepository<FeeStructure, Guid> feeStructureRepository,
		IRepository<TransactionCategory, Guid> incomeCategoryRepository,
		IRepository<AccountHead, Guid> accountHeadRepository,
		FeeCategoryManager feeCategoryManager,
		FeeHeadAccountingManager accountingManager,
		FeeHeadDependencyQuery dependencyQuery
	)
	{
		this.feeHeadRepository = feeHeadRepository;
		this.feeCategoryRepository = feeCategoryRepository;
		_ = feeStructureRepository;
		this.incomeCategoryRepository = incomeCategoryRepository;
		this.accountHeadRepository = accountHeadRepository;
		this.feeCategoryManager = feeCategoryManager;
		this.accountingManager = accountingManager;
		this.dependencyQuery = dependencyQuery;
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(academicYearId, input.FeeCategoryId, input.Name);
		await accountingManager.GetValidMappingAsync(academicYearId, input.IncomeCategoryId, creatingReference: true);

		var feeHead = new FeeHead(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.FeeCategoryId,
            input.Name.Trim(),
            null, // Module selection is retired for new fee heads.
            input.IsConcessionApplied,
            input.Description,
            input.IncomeCategoryId
        );

        await feeHeadRepository.InsertAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.View)]
    public async Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string? search = null, int page = 1, int pageSize = 10, string? filters = null)
    {
        var tableQuery = ListFilters.Parse(filters);
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 1000);
        var query = await feeHeadRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (feeCategoryId.HasValue)
        {
            query = query.Where(x => x.FeeCategoryId == feeCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch));
        }

		var categories = await feeCategoryRepository.GetQueryableAsync();
		var incomeCategories = await incomeCategoryRepository.GetQueryableAsync();
		var accounts = await accountHeadRepository.GetQueryableAsync();
		// Project scalars so filters and paging stay in SQL and do not hydrate navigation graphs.
		// The required category join retains the existing FeeHead AutoInclude visibility semantics.
		var rows = from head in query
			join category in categories on head.FeeCategoryId equals category.Id
			join income in incomeCategories on head.IncomeCategoryId equals income.Id into incomeGroup
			from income in incomeGroup.DefaultIfEmpty()
			join account in accounts on income.DefaultAccountHeadId equals (Guid?)account.Id into accountGroup
			from account in accountGroup.DefaultIfEmpty()
			select new FeeHeadResponse
			{
				Id = head.Id, AcademicYearId = head.AcademicYearId, FeeCategoryId = head.FeeCategoryId,
				FeeCategoryName = category.Name, IncomeCategoryId = head.IncomeCategoryId,
				IncomeCategoryName = income == null ? null : income.Name,
				IncomeAccountHeadId = account == null ? Guid.Empty : account.Id,
				IncomeAccountHeadName = account == null ? null : account.Name,
				Name = head.Name, Module = head.Module, IsConcessionApplied = head.IsConcessionApplied,
				Description = head.Description, IsActive = head.IsActive, DeactivationReason = head.DeactivationReason,
				CreatedAt = head.CreationTime, UpdatedAt = head.LastModificationTime
			};
		rows = ListFilters.Apply(rows, tableQuery);
		var totalCount = await AsyncExecuter.CountAsync(rows);
		var dtos = await AsyncExecuter.ToListAsync(rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		var dependencies = await dependencyQuery.GetAsync(dtos.Select(x => x.Id));
		foreach (var response in dtos)
		{
			response.Dependencies = dependencies[response.Id];
			response.IsUsedInDownstream = response.Dependencies.IsBlocked;
		}

        return new FeeHeadListResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtos
        };
    }

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.View)]
	public async Task<List<ActiveFeeHeadLookupDto>> GetActiveAsync(Guid academicYearId)
	{
		var feeHeadQuery = await feeHeadRepository.GetQueryableAsync();
		var categoryQuery = await feeCategoryRepository.GetQueryableAsync();
		var incomeCategoryQuery = await incomeCategoryRepository.GetQueryableAsync();
		var accountHeadQuery = await accountHeadRepository.GetQueryableAsync();

		return await AsyncExecuter.ToListAsync(
			from feeHead in feeHeadQuery
			join category in categoryQuery on feeHead.FeeCategoryId equals category.Id
			join incomeCategory in incomeCategoryQuery on feeHead.IncomeCategoryId equals incomeCategory.Id
			join mappedAccountHead in accountHeadQuery on incomeCategory.DefaultAccountHeadId equals (Guid?)mappedAccountHead.Id into accountHeadGroup
			from accountHead in accountHeadGroup.DefaultIfEmpty()
			where feeHead.AcademicYearId == academicYearId &&
				feeHead.IsActive &&
				category.AcademicYearId == academicYearId &&
				category.IsActive &&
				incomeCategory.AcademicYearId == academicYearId &&
				incomeCategory.CategoryType == TransactionType.Income &&
				incomeCategory.IsActive
			orderby category.Name, feeHead.Name
			select new ActiveFeeHeadLookupDto
			{
				Id = feeHead.Id,
				AcademicYearId = feeHead.AcademicYearId,
				FeeCategoryId = feeHead.FeeCategoryId,
				FeeCategoryName = category.Name,
				IncomeCategoryId = incomeCategory.Id,
				IncomeCategoryName = incomeCategory.Name,
				IncomeAccountHeadId = accountHead == null ? Guid.Empty : accountHead.Id,
				IncomeAccountHeadName = accountHead == null ? string.Empty : accountHead.Name,
				Name = feeHead.Name,
				Module = feeHead.Module,
				IsConcessionApplied = feeHead.IsConcessionApplied
			});
	}

	public async Task<FeeHeadResponse> GetByIdAsync(Guid id)
	{
		var feeHead = await feeHeadRepository.GetAsync(id);
		return await MapToResponseAsync(feeHead);
	}

	[UnitOfWork(isTransactional: true)]
    public async Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input)
    {
		var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(
			feeHead.AcademicYearId, input.FeeCategoryId, input.Name, feeHeadId);
		await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, input.IncomeCategoryId, creatingReference: true);
		if (feeHead.IncomeCategoryId != input.IncomeCategoryId &&
			(await GetUsedFeeHeadIdsAsync([feeHeadId])).Count > 0)
		{
			throw new BusinessException(FinanceFeeHeadErrorCodes.IncomeCategoryChangeBlocked);
		}

		feeHead.Update(input.FeeCategoryId, input.Name.Trim(), feeHead.Module, input.IsConcessionApplied,
			input.Description, input.IncomeCategoryId);

		await feeHeadRepository.UpdateAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
		var head = await feeHeadRepository.GetAsync(feeHeadId);
		var dependencies = (await dependencyQuery.GetAsync([feeHeadId]))[feeHeadId];
		if (dependencies.IsBlocked)
			throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeleteInUse, head.Name, dependencies);

		await feeHeadRepository.DeleteAsync(feeHeadId);

        return new MessageResponse
        {
            Message = "Fee head deleted successfully"
        };
    }

    public async Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input)
    {
        var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		await feeCategoryManager.EnsureFeeHeadStatusChangeAllowedAsync(feeHead.FeeCategoryId);

		if (input.IsActive)
		{
			await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, feeHead.IncomeCategoryId);
		}

		if (!input.IsActive)
		{
			var dependencies = (await dependencyQuery.GetAsync([feeHeadId]))[feeHeadId];
			if (dependencies.IsBlocked)
				throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeactivateInUse, feeHead.Name, dependencies);
		}

		feeHead.SetActiveStatus(input.IsActive, input.DeactivationReason);

		await feeHeadRepository.UpdateAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

	private async Task<HashSet<Guid>> GetUsedFeeHeadIdsAsync(List<Guid> feeHeadIds)
	{
		if (feeHeadIds.Count == 0)
		{
			return [];
		}

		return (await dependencyQuery.GetAsync(feeHeadIds)).Where(x => x.Value.IsBlocked).Select(x => x.Key).ToHashSet();
	}

	private async Task<FeeHeadResponse> MapToResponseAsync(FeeHead feeHead)
	{
		var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(feeHead);

		var category = await feeCategoryRepository.FindAsync(feeHead.FeeCategoryId);
		response.FeeCategoryName = category?.Name;
		var incomeCategory = await incomeCategoryRepository.FindAsync(feeHead.IncomeCategoryId);
		var accountHead = incomeCategory?.DefaultAccountHeadId is { } accountHeadId
			? await accountHeadRepository.FindAsync(accountHeadId) : null;
		response.IncomeCategoryName = incomeCategory?.Name;
		response.IncomeAccountHeadId = accountHead?.Id ?? Guid.Empty;
		response.IncomeAccountHeadName = accountHead?.Name;
		response.Dependencies = (await dependencyQuery.GetAsync([feeHead.Id]))[feeHead.Id];
		response.IsUsedInDownstream = response.Dependencies.IsBlocked;

		return response;
	}

}
