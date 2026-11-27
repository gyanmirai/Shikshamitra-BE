using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common.Files;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	public async Task<CashBulkDepositResult> BulkDepositAsync(CashBulkDeposit input)
	{
		await AuthorizationService.CheckAsync(P.Deposit);
		if (input.IdempotencyKey == Guid.Empty || input.Notes?.Length > 1000)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		CashCollection.EnsureMoney(input.ExpectedAmount);
		var amount = input.Amount ?? input.ExpectedAmount;
		CashCollection.EnsureMoney(amount);
		ValidateDocument(input);
		using var uow = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await sourceLock.AcquireAsync(input.AcademicYearId);
		var scoped = await ScopedQueryAsync(input.AcademicYearId);
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("bulk-deposit:" + Actor + ":" + JsonSerializer.Serialize(input))));
		// Discover the key independently of today's row scope. Losing access to the
		// anchor must never make an old command look new and spend a second pool.
		var prior = await AsyncExecuter.FirstOrDefaultAsync((await sources.WithDetailsAsync(x => x.Commands))
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.Commands.Any(c => c.Id == input.IdempotencyKey)));
		if (prior != null)
		{
			var result = JsonSerializer.Deserialize<CashBulkDepositReplay>(prior.Replay(input.IdempotencyKey, fingerprint)!)!;
			foreach (var sourceId in result.SourceIds) await RequireAsync(sourceId, input.AcademicYearId);
			await uow.CompleteAsync();
			return new CashBulkDepositResult(result.Amount, result.SourceCount);
		}
		var candidates = await AsyncExecuter.ToListAsync(scoped.Where(x => x.Amount > x.SpentAmount + x.DepositedAmount + x.InTransitAmount)
			.OrderBy(x => x.Id).Select(x => new { x.Id, Remaining = x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount }));
		var available = candidates.Sum(x => x.Remaining);
		if (available != input.ExpectedAmount) throw new BusinessException(CashManagementErrorCodes.Stale);
		if (amount > available) throw new BusinessException(CashManagementErrorCodes.InsufficientCash);

		var cashRows = new System.Collections.Generic.List<CashCollection>();
		var selectedAmount = 0m;
		foreach (var candidate in candidates)
		{
			if (selectedAmount >= amount) break;
			await sourceLock.AcquireAsync(candidate.Id);
			var cash = await RequireForUpdateAsync(candidate.Id, input.AcademicYearId);
			if (cash.RemainingAmount != candidate.Remaining) throw new BusinessException(CashManagementErrorCodes.Stale);
			cashRows.Add(cash);
			selectedAmount += cash.RemainingAmount;
		}
		var at = Now;
		// The selected year identifies custody ownership. Its closing date does not
		// prevent depositing outstanding historical cash at today's actual instant.
		var bank = await routing.RequireBankAsync(input.AcademicYearId, input.BankAccountId, at);
		string? storedFile = null;
		if (input.FileContent != null)
			storedFile = await LazyServiceProvider.LazyGetRequiredService<IFileStorageAppService>().UploadAsync(input.FileName!, input.FileContent);
		var remaining = amount;
		var bankLabel = BankLabel(bank);
		foreach (var cash in cashRows)
		{
			var pending = cash.Status == 0;
			// Preserve every earlier receiving-bank snapshot before the source's
			// current destination moves to the new bank (including legacy transit).
			if (cash.BankAccountId.HasValue)
				foreach (var priorDeposit in cash.Deposits)
				{
					var bankKey = "DepositBank:" + priorDeposit.Id.ToString("N");
					if (!cash.ExtraProperties.ContainsKey(bankKey))
						cash.ExtraProperties[bankKey] = JsonSerializer.Serialize(new CashBankOption(cash.BankAccountId.Value, cash.BankLabel ?? ""));
				}
			var deposited = Math.Min(remaining, cash.RemainingAmount);
			var deposit = cash.DepositAvailable(Actor, bank.Id, bankLabel, deposited, at, input.Notes);
			cash.ExtraProperties["DepositBank:" + deposit.Id.ToString("N")] = JsonSerializer.Serialize(new CashBankOption(bank.Id, bankLabel));
			cash.ExtraProperties["BulkDeposit:" + input.IdempotencyKey.ToString("N")] = JsonSerializer.Serialize(new { depositId = deposit.Id, bankAccountId = bank.Id, bankLabel, amount = deposited, actor = Actor, at, notes = input.Notes });
			if (storedFile != null)
			{
				cash.ExtraProperties["SupportingDocumentFile"] = storedFile;
				cash.ExtraProperties["SupportingDocumentName"] = Path.GetFileName(input.FileName);
			}
			if (pending) await ApplyDepositIncomeAsync(cash, bank, deposit);
			await sources.UpdateAsync(cash, autoSave: true);
			remaining -= deposited;
		}
		var response = new CashBulkDepositResult(amount, cashRows.Count);
		// One anchor owns batch replay, atomically with every allocation and document.
		await commands.AppendAsync(cashRows[0].Id, input.IdempotencyKey, fingerprint,
			JsonSerializer.Serialize(new CashBulkDepositReplay(amount, cashRows.Count, cashRows.Select(x => x.Id).ToArray())));
		await uow.CompleteAsync();
		return response;
	}
	private record CashBulkDepositReplay(decimal Amount, int SourceCount, Guid[] SourceIds);

	public async Task<CashDocument> GetDocumentAsync(Guid id, Guid academicYearId)
	{
		await AuthorizationService.CheckAsync(P.View);
		var cash = await RequireAsync(id, academicYearId);
		if (!cash.ExtraProperties.TryGetValue("SupportingDocumentFile", out var file) || file == null)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		var document = await LazyServiceProvider.LazyGetRequiredService<IFileStorageAppService>().GetAsync(file.ToString()!);
		return new CashDocument(cash.ExtraProperties["SupportingDocumentName"]?.ToString() ?? document.FileName, document.ContentType, document.Content);
	}

	private static void ValidateDocument(CashBulkDeposit input)
	{
		if (input.FileContent == null && input.FileName == null) return;
		var bytes = input.FileContent;
		if (bytes == null || bytes.Length is < 4 or > 5 * 1024 * 1024 || string.IsNullOrWhiteSpace(input.FileName) || input.FileName.Length > 255)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		var ext = Path.GetExtension(input.FileName).ToLowerInvariant();
		var valid = ext switch
		{
			".pdf" => bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-",
			".png" => bytes.Length >= 8 && bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
			".jpg" or ".jpeg" => bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
			_ => false
		};
		if (!valid) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
	}
}
