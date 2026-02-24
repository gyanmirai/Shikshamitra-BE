using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Expenses;
using ShikshaMitra.Application.Contracts.Finance.Expenses.Dtos;
using ShikshaMitra.Finance.Setting;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Expenses;

[RemoteService(false)]
public class ExpenseAppService : ApplicationService, IExpenseAppService
{
	private readonly IRepository<Expense, Guid> _expenseRepository;
	private readonly IRepository<ExpenseCategory, Guid> _expenseCategoryRepository;

	public ExpenseAppService(
		IRepository<Expense, Guid> expenseRepository,
		IRepository<ExpenseCategory, Guid> expenseCategoryRepository)
	{
		_expenseRepository = expenseRepository;
		_expenseCategoryRepository = expenseCategoryRepository;
	}

	public async Task<ExpenseListResponse> GetListAsync(Guid academicYearId, GetExpensesRequest input)
	{
		var expenseQuery = await _expenseRepository.GetQueryableAsync();
		var categoryQuery = await _expenseCategoryRepository.GetQueryableAsync();

		var joinedQuery =
			from expense in expenseQuery
			join category in categoryQuery on expense.CategoryId equals category.Id
			where expense.AcademicYearId == academicYearId
			select new { expense, category };

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var searchLower = input.Search.ToLower();
			joinedQuery = joinedQuery.Where(x => x.expense.Name.ToLower().Contains(searchLower));
		}

		var totalCount = await AsyncExecuter.CountAsync(joinedQuery);

		var items = await AsyncExecuter.ToListAsync(
			joinedQuery
				.OrderByDescending(x => x.expense.CreationTime)
				.Skip((input.Page - 1) * input.PageSize)
				.Take(input.PageSize)
		);

		var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);

		var listItems = items.Select(x => new ExpenseListItem
		{
			Id = x.expense.Id,
			CategoryId = x.expense.CategoryId,
			CategoryName = x.category.Name,
			CategoryTags = ParseTags(x.expense.CategoryTags),
			Name = x.expense.Name,
			Amount = (double) x.expense.Amount,
			PaymentMethod = x.expense.PaymentMethod,
			PaymentDate = x.expense.PaymentDate,
			Notes = x.expense.Notes,
			CreatedAt = x.expense.CreationTime
		}).ToList();

		return new ExpenseListResponse
		{
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = totalPages,
			Items = listItems
		};
	}

	public async Task<ExpenseCreateUpdateResponse> CreateAsync(Guid academicYearId, ExpenseCreateRequest input)
	{
		var category = await _expenseCategoryRepository.FindAsync(input.CategoryId);
		if (category == null)
		{
			throw new BusinessException("ShikshaMitra:ExpenseCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);
		}

		var expense = new Expense(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.CategoryId,
			input.Name,
			SerializeTags(input.CategoryTags),
			(decimal) input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes
		);

		await _expenseRepository.InsertAsync(expense, autoSave: true);

		return BuildCreateUpdateResponse(expense, category);
	}

	public async Task<ExpenseDetailsResponse> GetByIdAsync(Guid expenseId)
	{
		var expense = await _expenseRepository.GetAsync(expenseId);
		var category = await _expenseCategoryRepository.GetAsync(expense.CategoryId);

		// Fetch all expenses for the same category in the same academic year
		var expenseQuery = await _expenseRepository.GetQueryableAsync();
		var relatedExpenses = await AsyncExecuter.ToListAsync(
			expenseQuery.Where(x =>
				x.CategoryId == expense.CategoryId &&
				x.AcademicYearId == expense.AcademicYearId)
		);

		var totalAmount = relatedExpenses.Sum(x => x.Amount);
		var categoryTags = ParseTags(category.Tags ?? string.Empty);

		return new ExpenseDetailsResponse
		{
			ExpenseInformation = new ExpenseInformation
			{
				ExpenseCategoryId = category.Id,
				ExpenseCategoryName = category.Name,
				CategoryTags = categoryTags,
				TotalAmountSpent = (double) totalAmount
			},
			ExpenseList = relatedExpenses.Select(x => new ExpenseTransaction
			{
				TransactionId = x.Id,
				ExpenseName = x.Name,
				SpentDate = x.PaymentDate,
				PaymentMethod = x.PaymentMethod,
				Amount = (double) x.Amount
			}).ToList()
		};
	}

	public async Task<ExpenseCreateUpdateResponse> UpdateAsync(Guid expenseId, ExpenseUpdateRequest input)
	{
		var expense = await _expenseRepository.GetAsync(expenseId);

		var category = await _expenseCategoryRepository.FindAsync(input.CategoryId);
		if (category == null)
		{
			throw new BusinessException("ShikshaMitra:ExpenseCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);
		}

		expense.Update(
			input.CategoryId,
			input.Name,
			SerializeTags(input.CategoryTags),
			(decimal) input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes
		);

		await _expenseRepository.UpdateAsync(expense, autoSave: true);

		return BuildCreateUpdateResponse(expense, category);
	}

	public async Task<ExpenseDeleteResponse> DeleteAsync(Guid expenseId)
	{
		var expense = await _expenseRepository.GetAsync(expenseId);
		await _expenseRepository.DeleteAsync(expense, autoSave: true);

		return new ExpenseDeleteResponse
		{
			Success = true,
			Message = "Expense deleted successfully"
		};
	}

	// ────────────────────────────────────────────────
	// Helpers
	// ────────────────────────────────────────────────

	private static ExpenseCreateUpdateResponse BuildCreateUpdateResponse(Expense expense, ExpenseCategory category)
	{
		return new ExpenseCreateUpdateResponse
		{
			Success = true,
			Data = new ExpenseResponseData
			{
				Id = expense.Id,
				CategoryId = expense.CategoryId,
				CategoryName = category.Name,
				CategoryTags = ParseTags(expense.CategoryTags),
				Name = expense.Name,
				Amount = (double) expense.Amount,
				PaymentMethod = expense.PaymentMethod,
				PaymentDate = expense.PaymentDate,
				Notes = expense.Notes,
				CreatedAt = expense.CreationTime
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
