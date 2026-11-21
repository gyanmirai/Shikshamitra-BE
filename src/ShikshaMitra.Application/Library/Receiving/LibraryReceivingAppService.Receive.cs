using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Receiving;

public partial class LibraryReceivingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	[UnitOfWork(true)]
	public async Task<LibraryStockReceiptDto> ReceiveAsync(LibraryReceiveCommand input)
	{
		await EnsureAccessAsync(true);
		ValidateCommand(input);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var normalized = LibraryReceivingInputRules.Normalize(input.Receipt);
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized))));
		var tenant = CurrentTenant.Id!.Value;
		var actor = CurrentUser.Id!.Value;
		var accepted = await commands.FindAsync(row => row.TenantId == tenant && row.ActorId == actor && row.IdempotencyKey == input.IdempotencyKey);
		if (accepted != null)
		{
			if (!string.Equals(accepted.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
				throw new BusinessException(LibraryErrorCodes.ReceivingCommandConflict);
			return await ReadAsync(accepted.ReceiptId);
		}
		var preview = await previewBuilder.BuildAsync(tenant, normalized);
		if (!preview.CanReceive) throw new BusinessException(LibraryErrorCodes.ReceivingInvalid).WithData("Errors", preview.Errors);
		if (!string.Equals(input.Revision, preview.Revision, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.ReceivingPlanChanged);
		var stock = await stockFactory.BuildAsync(tenant, normalized);
		var at = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var receipt = new LibraryStockReceipt(GuidGenerator.Create(), tenant, normalized.ReceivedOn, normalized.Source!.Value,
			normalized.SourceName, normalized.Reference, actor, at, stock.Lines);
		await copies.InsertManyAsync(stock.Copies);
		await receipts.InsertAsync(receipt);
		await commands.InsertAsync(new(GuidGenerator.Create(), tenant, actor, receipt.Id, input.IdempotencyKey, fingerprint, at));
		var bookIds = stock.Lines.Select(line => line.BookId).Distinct().ToArray();
		var titles = await books.GetListAsync(book => book.TenantId == tenant && Enumerable.Contains(bookIds, book.Id));
		foreach (var book in titles)
			book.NumberOfCopies = stock.ExistingCounts.GetValueOrDefault(book.Id) + stock.Copies.Count(copy => copy.BookId == book.Id);
		await books.UpdateManyAsync(titles);
		await units.Current!.SaveChangesAsync();
		foreach (var bookId in bookIds) await reservations.RebalanceAsync(bookId);
		return await ReadAsync(receipt.Id);
	}

	private static void ValidateCommand(LibraryReceiveCommand input)
	{
		if (input == null || input.Receipt == null || input.Receipt.Lines == null ||
			input.Receipt.Lines.Count is < 1 or > LibraryStockReceiptRules.MaximumLines || input.Receipt.Lines.Any(line => line == null) ||
			input.Receipt.Lines.Sum(line => (long)Math.Max(0, line.Quantity)) > LibraryStockReceiptRules.MaximumCopies ||
			input.Receipt.Lines.Sum(line => (long)(line.Accessions?.Count ?? 0)) > LibraryStockReceiptRules.MaximumCopies ||
			string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100 ||
			input.IdempotencyKey != input.IdempotencyKey.Trim() || input.IdempotencyKey.Any(char.IsControl) ||
			input.Revision == null || input.Revision.Length != 64)
			throw new BusinessException(LibraryErrorCodes.ReceivingInvalid);
	}
}
