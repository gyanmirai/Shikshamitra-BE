using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	/// <summary>A pooled payment is visible only when every funding source is accessible.</summary>
	public async Task<IQueryable<Transaction>> FilterPooledCashTransactionsAsync(IQueryable<Transaction> query, Guid yearId)
	{
		query = query.Where(t => t.TenantId == CurrentTenant.Id && t.AcademicYearId == yearId);
		var candidates = await AsyncExecuter.ToListAsync(query.Where(t => t.PaymentMethod == 0 &&
			t.TransactionType == TransactionType.Expense && t.CashCollectionId.HasValue)
			.Select(t => new { t.Id, t.ExtraProperties }));
		var allocations = new Dictionary<Guid, Guid[]>();
		var denied = new HashSet<Guid>();
		foreach (var candidate in candidates)
		{
			if (!candidate.ExtraProperties.TryGetValue("CashAllocations", out var evidence)) continue;
			try
			{
				using var json = JsonDocument.Parse(evidence?.ToString() ?? "null");
				var sourceIds = json.RootElement.EnumerateArray().Select(x => x.GetProperty("SourceId").GetGuid()).Distinct().ToArray();
				if (sourceIds.Length == 0 || sourceIds.Contains(Guid.Empty)) denied.Add(candidate.Id);
				else allocations[candidate.Id] = sourceIds;
			}
			catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
			{
				denied.Add(candidate.Id);
			}
		}
		if (allocations.Count > 0)
		{
			var sourceIds = allocations.Values.SelectMany(x => x).Distinct().ToArray();
			var students = await GetScopedStudentIdsQueryAsync();
			var accounts = await GetScopedFeeAccountIdsQueryAsync();
			var manual = await IsAcademicContextAccessibleAsync(yearId, null, null, null, null);
			var sources = await LazyServiceProvider.LazyGetRequiredService<IRepository<CashCollection, Guid>>().GetQueryableAsync();
			var accessible = (await AsyncExecuter.ToListAsync(sources.Where(c => c.TenantId == CurrentTenant.Id &&
				c.AcademicYearId == yearId && Enumerable.Contains(sourceIds, c.Id) &&
				(c.FinanceFeeAccountId.HasValue ? accounts.Contains(c.FinanceFeeAccountId.Value) :
					c.StudentId.HasValue ? students.Contains(c.StudentId.Value) : manual)).Select(c => c.Id))).ToHashSet();
			foreach (var allocation in allocations)
				if (allocation.Value.Any(id => !accessible.Contains(id))) denied.Add(allocation.Key);
		}
		var deniedIds = denied.ToArray();
		return deniedIds.Length == 0 ? query : query.Where(t => !Enumerable.Contains(deniedIds, t.Id));
	}

	public async Task EnsurePooledCashTransactionAccessibleAsync(Transaction transaction)
	{
		var query = (await LazyServiceProvider.LazyGetRequiredService<IRepository<Transaction, Guid>>().GetQueryableAsync())
			.Where(t => t.Id == transaction.Id);
		if (!await AsyncExecuter.AnyAsync(await FilterPooledCashTransactionsAsync(query, transaction.AcademicYearId)))
			throw new AbpAuthorizationException();
	}
}
