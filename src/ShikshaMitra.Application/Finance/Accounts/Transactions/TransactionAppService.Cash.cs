using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Transactions;

public partial class TransactionAppService
{
	private async Task RouteCashAsync(Transaction transaction, TransactionCreateRequest input)
	{
		if (input.PaymentMethod != 0) return;
		var sources = LazyServiceProvider.LazyGetRequiredService<IRepository<CashCollection, Guid>>();
		if (transaction.TransactionType == TransactionType.Income)
		{
			if (input.CashCollectionId.HasValue || input.CashConcurrencyStamp != null)
				throw new BusinessException(CashManagementErrorCodes.InvalidSource);
			var cash = new CashCollection(GuidGenerator.Create(), CurrentTenant.Id, transaction.AcademicYearId, transaction.Id,
				$"manual:{transaction.Id:N}", transaction.Id, input.PaymentReference?.Trim() is { Length: > 0 } reference ? reference : input.Name,
				input.Counterparty?.Trim() is { Length: > 0 } payer ? payer : input.Name, input.Amount, input.PaymentDate,
				CurrentUser.Id ?? throw new AbpAuthorizationException(), null, null);
			await sources.InsertAsync(cash, autoSave: true);
			transaction.LinkCashCollection(cash.Id, pendingIncome: true);
			return;
		}
		if (!input.CashCollectionId.HasValue || input.CashCollectionId == Guid.Empty || string.IsNullOrWhiteSpace(input.CashConcurrencyStamp) ||
			string.IsNullOrWhiteSpace(input.Counterparty) || string.IsNullOrWhiteSpace(input.PaymentReference))
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		await LazyServiceProvider.LazyGetRequiredService<ICashCollectionLock>().AcquireAsync(input.CashCollectionId.Value);
		var source = await AsyncExecuter.FirstOrDefaultAsync((await sources.WithDetailsAsync(x => x.Expenses))
			.Where(x => x.Id == input.CashCollectionId && x.TenantId == CurrentTenant.Id && x.AcademicYearId == transaction.AcademicYearId))
			?? throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		if (source.FinanceFeeAccountId.HasValue || source.StudentId.HasValue)
			await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(source.StudentId, source.FinanceFeeAccountId);
		else await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(source.AcademicYearId, null, null, null, null);
		source.EnsureStamp(input.CashConcurrencyStamp);
		if (input.PaymentDate < source.CollectedAt || input.PaymentDate > new DateTimeOffset(Clock.Now.ToUniversalTime()))
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		source.Spend(transaction.Id, transaction.Name, transaction.Amount, input.PaymentDate);
		transaction.LinkCashCollection(source.Id, studentId: source.StudentId, financeFeeAccountId: source.FinanceFeeAccountId);
		transaction.ExtraProperties["CashConcurrencyStamp"] = input.CashConcurrencyStamp;
		await sources.UpdateAsync(source, autoSave: true);
	}
}
