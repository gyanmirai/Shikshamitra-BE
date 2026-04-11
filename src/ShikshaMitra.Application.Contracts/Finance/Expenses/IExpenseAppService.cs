using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Expenses.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Expenses;

public interface IExpenseAppService : IApplicationService
{
	Task<ExpenseListResponse> GetListAsync(Guid academicYearId, GetExpensesRequest input);

	Task<ExpenseCreateUpdateResponse> CreateAsync(Guid academicYearId, ExpenseCreateRequest input);

	Task<ExpenseDetailsResponse> GetByIdAsync(Guid expenseId);

	Task<ExpenseCreateUpdateResponse> UpdateAsync(Guid expenseId, ExpenseUpdateRequest input);

	Task<ExpenseDeleteResponse> DeleteAsync(Guid expenseId);
}
