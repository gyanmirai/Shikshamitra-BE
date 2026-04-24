using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Transactions;

public interface ITransactionAppService : IApplicationService
{
	Task<TransactionListResponse> GetListAsync(Guid academicYearId, GetTransactionsRequest input, TransactionType transactionType);

	Task<TransactionCreateUpdateResponse> CreateAsync(Guid academicYearId, TransactionCreateRequest input, TransactionType transactionType);

	Task<TransactionDetailsResponse> GetByIdAsync(Guid transactionId, TransactionType transactionType);
	Task<TransactionCreateUpdateResponse> UpdateAsync(Guid transactionId, TransactionUpdateRequest input, TransactionType transactionType);

	Task<TransactionDeleteResponse> DeleteAsync(Guid transactionId, TransactionType transactionType);
}
