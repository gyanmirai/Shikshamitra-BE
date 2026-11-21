using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Receiving;

internal sealed record LibraryReceivingStock(List<BookCopy> Copies, List<LibraryStockLineEvidence> Lines,
	Dictionary<Guid, int> ExistingCounts);

public sealed class LibraryReceivingCopyFactory(IRepository<BookCopy, Guid> copies,
	IAsyncQueryableExecuter asyncExecuter, IGuidGenerator guids) : ITransientDependency
{
	internal async Task<LibraryReceivingStock> BuildAsync(Guid tenantId, LibraryReceivingInput input)
	{
		var bookIds = input.Lines.Select(line => line.BookId).Distinct().ToArray();
		var stock = await asyncExecuter.ToListAsync((await copies.GetQueryableAsync())
			.Where(copy => copy.TenantId == tenantId && Enumerable.Contains(bookIds, copy.BookId)).GroupBy(copy => copy.BookId)
			.Select(group => new { BookId = group.Key, Count = group.Count(), Maximum = group.Max(copy => copy.CopyNumber) }));
		var numbers = stock.ToDictionary(row => row.BookId, row => row.Maximum);
		var existingAccessions = await asyncExecuter.ToListAsync((await copies.GetQueryableAsync())
			.Where(copy => copy.TenantId == tenantId && copy.NormalizedAccessionNumber.StartsWith("ACC-"))
			.Select(copy => copy.NormalizedAccessionNumber));
		var manual = input.Lines.Where(line => line.Accessions != null).SelectMany(line => line.Accessions!)
			.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
		var automatic = LibraryAccessionNumbers.Allocate(existingAccessions.Concat(manual), input.Lines.Sum(line => line.Quantity) - manual.Count);
		var nextAutomatic = 0;
		var result = new LibraryReceivingStock([], [], stock.ToDictionary(row => row.BookId, row => row.Count));
		foreach (var line in input.Lines)
		{
			var received = new List<LibraryReceivedCopyEvidence>(line.Quantity);
			for (var index = 0; index < line.Quantity; index++)
			{
				var accession = line.Accessions?[index];
				if (string.IsNullOrWhiteSpace(accession)) accession = automatic[nextAutomatic++];
				var previous = numbers.GetValueOrDefault(line.BookId);
				if (previous == int.MaxValue) throw new BusinessException(LibraryErrorCodes.InvalidCopyIdentity);
				var number = previous + 1;
				numbers[line.BookId] = number;
				var copy = new BookCopy(guids.Create(), line.BookId, number, accession, line.SectionId, line.ShelfId, tenantId);
				result.Copies.Add(copy);
				received.Add(new(copy.Id, copy.AccessionNumber, copy.CopyNumber));
			}
			LibraryReceivingInputRules.TryCost(line.UnitCost, line.Currency, out var cost);
			result.Lines.Add(new(guids.Create(), line.BookId, line.SectionId, line.ShelfId, line.Quantity, cost, line.Currency, received));
		}
		return result;
	}
}
