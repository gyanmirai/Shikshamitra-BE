using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Incomes.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Incomes;

public interface IIncomeAppService : IApplicationService
{
	Task<IncomeListResponse> GetListAsync(Guid academicYearId, GetIncomesRequest input);

	Task<IncomeCreateUpdateResponse> CreateAsync(Guid academicYearId, IncomeCreateRequest input);

	Task<IncomeDetailsResponse> GetByIdAsync(Guid incomeId);

	Task<IncomeCreateUpdateResponse> UpdateAsync(Guid incomeId, IncomeUpdateRequest input);

	Task<IncomeDeleteResponse> DeleteAsync(Guid incomeId);
}
