using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Incomes.Dtos;
using ShikshaMitra.Finance.Accounts.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Transactions;

[RemoteService(false)]
public class TransactionAppService : ApplicationService, IIncomeAppService
{
	private readonly IRepository<Income, Guid> _incomeRepository;
	private readonly IRepository<TransactionCategory, Guid> _incomeCategoryRepository;

	public TransactionAppService(
		IRepository<Income, Guid> incomeRepository,
		IRepository<TransactionCategory, Guid> incomeCategoryRepository)
	{
		_incomeRepository = incomeRepository;
		_incomeCategoryRepository = incomeCategoryRepository;
	}

	public async Task<IncomeListResponse> GetListAsync(Guid academicYearId, GetIncomesRequest input)
	{
		var incomeQuery = await _incomeRepository.GetQueryableAsync();
		var categoryQuery = await _incomeCategoryRepository.GetQueryableAsync();

		var joinedQuery =
			from income in incomeQuery
			join category in categoryQuery on income.CategoryId equals category.Id
			where income.AcademicYearId == academicYearId
			select new { income, category };

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var searchLower = input.Search.ToLower();
			joinedQuery = joinedQuery.Where(x => x.income.Name.ToLower().Contains(searchLower));
		}

		var totalCount = await AsyncExecuter.CountAsync(joinedQuery);

		var items = await AsyncExecuter.ToListAsync(
			joinedQuery
				.OrderByDescending(x => x.income.CreationTime)
				.Skip((input.Page - 1) * input.PageSize)
				.Take(input.PageSize)
		);

		var totalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize);

		var listItems = items.Select(x => new IncomeListItem
		{
			Id = x.income.Id,
			CategoryId = x.income.CategoryId,
			CategoryName = x.category.Name,
			CategoryTags = ParseTags(x.income.CategoryTags),
			Name = x.income.Name,
			Amount = (double)x.income.Amount,
			PaymentMethod = x.income.PaymentMethod,
			PaymentDate = x.income.PaymentDate,
			Notes = x.income.Notes,
			CreatedAt = x.income.CreationTime
		}).ToList();

		return new IncomeListResponse
		{
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = totalPages,
			Items = listItems
		};
	}

	public async Task<IncomeCreateUpdateResponse> CreateAsync(Guid academicYearId, IncomeCreateRequest input)
	{
		var category = await _incomeCategoryRepository.FindAsync(input.CategoryId);
		if (category == null)
		{
			throw new BusinessException("ShikshaMitra:IncomeCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);
		}

		var income = new Income(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.CategoryId,
			input.Name,
			SerializeTags(input.CategoryTags),
			(decimal)input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes
		);

		await _incomeRepository.InsertAsync(income, autoSave: true);

		return BuildCreateUpdateResponse(income, category);
	}

	public async Task<IncomeDetailsResponse> GetByIdAsync(Guid incomeId)
	{
		var income = await _incomeRepository.GetAsync(incomeId);
		var category = await _incomeCategoryRepository.GetAsync(income.CategoryId);

		// Fetch all incomes for the same category in the same academic year
		var incomeQuery = await _incomeRepository.GetQueryableAsync();
		var relatedIncomes = await AsyncExecuter.ToListAsync(
			incomeQuery.Where(x =>
				x.CategoryId == income.CategoryId &&
				x.AcademicYearId == income.AcademicYearId)
		);

		var totalAmount = relatedIncomes.Sum(x => x.Amount);
		var categoryTags = ParseTags(category.Tags ?? string.Empty);

		return new IncomeDetailsResponse
		{
			IncomeInformation = new IncomeInformation
			{
				IncomeCategoryId = category.Id,
				IncomeCategoryName = category.Name,
				CategoryTags = categoryTags,
				TotalAmountReceived = (double)totalAmount
			},
			IncomeList = relatedIncomes.Select(x => new IncomeTransaction
			{
				TransactionId = x.Id,
				IncomeName = x.Name,
				ReceivedDate = x.PaymentDate,
				PaymentMethod = x.PaymentMethod,
				Amount = (double)x.Amount
			}).ToList()
		};
	}

	public async Task<IncomeCreateUpdateResponse> UpdateAsync(Guid incomeId, IncomeUpdateRequest input)
	{
		var income = await _incomeRepository.GetAsync(incomeId);

		var category = await _incomeCategoryRepository.FindAsync(input.CategoryId);
		if (category == null)
		{
			throw new BusinessException("ShikshaMitra:IncomeCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);
		}

		income.Update(
			input.CategoryId,
			input.Name,
			SerializeTags(input.CategoryTags),
			(decimal)input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes
		);

		await _incomeRepository.UpdateAsync(income, autoSave: true);

		return BuildCreateUpdateResponse(income, category);
	}

	public async Task<IncomeDeleteResponse> DeleteAsync(Guid incomeId)
	{
		var income = await _incomeRepository.GetAsync(incomeId);
		await _incomeRepository.DeleteAsync(income, autoSave: true);

		return new IncomeDeleteResponse
		{
			Success = true,
			Message = "Income deleted successfully"
		};
	}

	// ────────────────────────────────────────────────
	// Helpers
	// ────────────────────────────────────────────────

	private static IncomeCreateUpdateResponse BuildCreateUpdateResponse(Income income, TransactionCategory category)
	{
		return new IncomeCreateUpdateResponse
		{
			Success = true,
			Data = new IncomeResponseData
			{
				Id = income.Id,
				CategoryId = income.CategoryId,
				CategoryName = category.Name,
				CategoryTags = ParseTags(income.CategoryTags),
				Name = income.Name,
				Amount = (double)income.Amount,
				PaymentMethod = income.PaymentMethod,
				PaymentDate = income.PaymentDate,
				Notes = income.Notes,
				CreatedAt = income.CreationTime
			}
		};
	}

	private static List<string> ParseTags(string tags)
	{
		if (string.IsNullOrWhiteSpace(tags))
			return new List<string>();

		return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private static string SerializeTags(List<string>? tags)
	{
		if (tags == null || tags.Count == 0)
			return string.Empty;

		return string.Join(",", tags);
	}
}
