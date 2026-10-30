using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
		if (!input.CashCollectionId.HasValue && input.CashConcurrencyStamp == null)
		{
			await RoutePooledCashExpenseAsync(transaction, input, sources);
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

	private async Task RoutePooledCashExpenseAsync(Transaction transaction, TransactionCreateRequest input, IRepository<CashCollection, Guid> sources)
	{
		if (string.IsNullOrWhiteSpace(input.Counterparty)) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		var students = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var accounts = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
		var manual = await financeScopeAccessService.IsAcademicContextAccessibleAsync(transaction.AcademicYearId, null, null, null, null);
		var sourceFacts = await AsyncExecuter.ToListAsync((await sources.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == transaction.AcademicYearId &&
			x.Amount > x.SpentAmount + x.DepositedAmount + x.InTransitAmount &&
			(x.FinanceFeeAccountId.HasValue ? accounts.Contains(x.FinanceFeeAccountId.Value) : x.StudentId.HasValue ? students.Contains(x.StudentId.Value) : manual))
			.Select(x => new { x.Id, x.CollectedAt }));
		var candidates = sourceFacts.Where(x => x.CollectedAt <= input.PaymentDate)
			.OrderBy(x => x.CollectedAt).ThenBy(x => x.Id).Select(x => x.Id).ToArray();
		var remaining = transaction.Amount;
		var allocations = new List<CashAllocation>();
		foreach (var id in candidates)
		{
			if (remaining == 0) break;
			await LazyServiceProvider.LazyGetRequiredService<ICashCollectionLock>().AcquireAsync(id);
			using var tracking = sources.EnableTracking();
			var cash = await AsyncExecuter.FirstAsync((await sources.WithDetailsAsync(x => x.Expenses)).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.AcademicYearId == transaction.AcademicYearId));
			var amount = Math.Min(remaining, cash.RemainingAmount);
			if (amount <= 0) continue;
			// Expense record IDs are globally unique; each source gets its own immutable allocation ID.
			var allocationId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{transaction.Id:N}:{cash.Id:N}"))[..16]);
			cash.SpendAvailable(allocationId, transaction.Name, amount, input.PaymentDate);
			cash.ExtraProperties["ExpenseTransaction:" + allocationId.ToString("N")] = transaction.Id.ToString();
			allocations.Add(new CashAllocation(cash.Id, allocationId, amount));
			if (!transaction.CashCollectionId.HasValue) transaction.LinkCashCollection(cash.Id, studentId: cash.StudentId, financeFeeAccountId: cash.FinanceFeeAccountId);
			await sources.UpdateAsync(cash, autoSave: true);
			remaining -= amount;
		}
		if (remaining > 0) throw new BusinessException(CashManagementErrorCodes.InsufficientCash);
		transaction.ExtraProperties["CashAllocations"] = JsonSerializer.Serialize(allocations);
	}
	private async Task EnsurePooledReplayScopeAsync(Transaction transaction)
	{
		if (!transaction.ExtraProperties.TryGetValue("CashAllocations", out var evidence)) return;
		var allocations = JsonSerializer.Deserialize<List<CashAllocation>>(evidence!.ToString()!)!;
		var sources = LazyServiceProvider.LazyGetRequiredService<IRepository<CashCollection, Guid>>();
		foreach (var allocation in allocations)
		{
			var cash = await sources.GetAsync(allocation.SourceId);
			if (cash.TenantId != CurrentTenant.Id || cash.AcademicYearId != transaction.AcademicYearId) throw new AbpAuthorizationException();
			if (cash.StudentId.HasValue || cash.FinanceFeeAccountId.HasValue)
				await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(cash.StudentId, cash.FinanceFeeAccountId);
			else await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(cash.AcademicYearId, null, null, null, null);
		}
	}
	private record CashAllocation(Guid SourceId, Guid AllocationId, decimal Amount);
}
