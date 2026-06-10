using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;

namespace ShikshaMitra.Finance.Accounts.Settings;

[RemoteService(false)]
public class TransactionCategoryAppService : ApplicationService, ITransactionCategoryAppService
{
	private readonly IRepository<TransactionCategory, Guid> _transactionCategoryRepository;

	public TransactionCategoryAppService(IRepository<TransactionCategory, Guid> transactionCategoryRepository)
	{
		_transactionCategoryRepository = transactionCategoryRepository;
	}

	public async Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId, CreateTransactionCategoryRequest input, TransactionType categoryType)
	{
		var category = new TransactionCategory(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			categoryType,
			input.Name,
			input.Description,
			input.Tags
		);

		await _transactionCategoryRepository.InsertAsync(category);

		return MapToResponse(category);
	}

	public async Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId, TransactionType categoryType, int page = 1, int pageSize = 10, string? search = null)
	{
		var query = await _transactionCategoryRepository.GetQueryableAsync();

		query = query.Where(x => x.AcademicYearId == academicYearId && (x.CategoryType == null || x.CategoryType == categoryType));

		if (!search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.Name.Contains(search));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);

		var items = await AsyncExecuter.ToListAsync(
			query
			.OrderByDescending(x => x.CreationTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
		);

		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		return new PaginatedTransactionCategoryResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items.Select(MapToResponse).ToList()
		};
	}

	public async Task<TransactionCategoryResponse> GetAsync(Guid id, TransactionType categoryType)
	{
		var category = await _transactionCategoryRepository.GetAsync(id);
		EnsureCategoryType(category, categoryType);
		return MapToResponse(category);
	}

	public async Task<TransactionCategoryResponse> UpdateAsync(Guid id, UpdateTransactionCategoryRequest input, TransactionType categoryType)
	{
		var category = await _transactionCategoryRepository.GetAsync(id);
		EnsureCategoryType(category, categoryType);

		category.Name = input.Name;
		category.Description = input.Description;
		category.ClearTags();
		foreach (var tag in input.Tags)
		{
			category.AddTag(tag);
		}

		await _transactionCategoryRepository.UpdateAsync(category);

		return MapToResponse(category);
	}

	public async Task DeleteAsync(Guid id, TransactionType categoryType)
	{
		var category = await _transactionCategoryRepository.GetAsync(id);
		EnsureCategoryType(category, categoryType);
		await _transactionCategoryRepository.DeleteAsync(category);
	}

	private static void EnsureCategoryType(TransactionCategory category, TransactionType expected)
	{
		if (category.CategoryType.HasValue && category.CategoryType.Value != expected)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch")
				.WithData("CategoryId", category.Id)
				.WithData("Expected", expected)
				.WithData("Actual", category.CategoryType);
	}

	private TransactionCategoryResponse MapToResponse(TransactionCategory entity)
	{
		return new TransactionCategoryResponse
		{
			Id = entity.Id,
			Name = entity.Name,
			Description = entity.Description,
			Tags = entity.Tags != null && entity.Tags.Any() ? entity.Tags.Select(t => t.Value).ToList() : new List<string>()
		};
	}
}
