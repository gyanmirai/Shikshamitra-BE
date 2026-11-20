using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public partial class BookAppService
{
	private async Task ReconcileCopiesAsync(Guid bookId, List<BookCopyItem> input)
	{
		var existing = await bookCopyRepository.GetListAsync(x => x.BookId == bookId);
		var byId = existing.ToDictionary(x => x.Id);
		var byAccession = existing.ToDictionary(x => x.AccessionNumber, StringComparer.OrdinalIgnoreCase);
		var retainedIds = new HashSet<Guid>();
		foreach (var item in input)
		{
			BookCopy? matched = null;
			if (item.CopyId.HasValue)
			{
				if (!byId.TryGetValue(item.CopyId.Value, out matched))
				{
					throw LibraryException(LibraryErrorCodes.InvalidCopyIdentity, "The copy identity does not belong to this book.");
				}
				if (!string.IsNullOrEmpty(item.AccessionNumber) &&
					!string.Equals(item.AccessionNumber, matched.AccessionNumber, StringComparison.OrdinalIgnoreCase))
				{
					throw LibraryException(LibraryErrorCodes.AccessionImmutable, "An existing copy's accession number cannot be changed. Add a new copy instead.");
				}
			}
			else if (!string.IsNullOrEmpty(item.AccessionNumber))
			{
				byAccession.TryGetValue(item.AccessionNumber, out matched);
			}

			if (matched == null) continue;
			if (item.SectionId != matched.SectionId || item.ShelfId != matched.ShelfId)
				throw new BusinessException(LibraryErrorCodes.CopyMoveRequired,
					"Use Move copy with a reason to change an existing copy's section or shelf.");
			if (!retainedIds.Add(matched.Id))
			{
				throw LibraryException(LibraryErrorCodes.InvalidCopyIdentity, "Each existing copy can appear only once in the copy list.");
			}
			item.CopyId = matched.Id;
			item.AccessionNumber = matched.AccessionNumber;
		}

		var removed = existing.Where(x => !retainedIds.Contains(x.Id)).ToList();
		await EnsureCopiesCanBeRemovedAsync(removed);
		await AllocateAccessionsAsync(input);
		await EnsureCopyPayloadIsValidAsync(bookId, input, allowSameBookAccession: true);

		var retained = new List<BookCopy>();
		var added = new List<BookCopy>();
		foreach (var item in input)
		{
			if (item.CopyId.HasValue)
			{
				var copy = byId[item.CopyId.Value];
				copy.CopyNumber = item.CopyNumber;
				copy.SectionId = item.SectionId;
				copy.ShelfId = item.ShelfId;
				retained.Add(copy);
			}
			else
			{
				added.Add(new BookCopy(GuidGenerator.Create(), bookId, item.CopyNumber,
					item.AccessionNumber!, item.SectionId, item.ShelfId, CurrentTenant.Id));
			}
		}

		if (removed.Count > 0) await bookCopyRepository.DeleteManyAsync(removed);
		if (retained.Count > 0) await bookCopyRepository.UpdateManyAsync(retained);
		if (added.Count > 0) await bookCopyRepository.InsertManyAsync(added);
	}

	private async Task EnsureCopiesCanBeRemovedAsync(IReadOnlyCollection<BookCopy> copies)
	{
		if (copies.Count == 0) return;
		var retention = await copyRetention.ReadAsync(copies);
		if (retention.Values.Any(facts => facts.BlockedReason != null))
		{
			throw LibraryException(LibraryErrorCodes.CopyHasHistory,
				CopyRemovalBlockedReason);
		}
	}

	private async Task AllocateAccessionsAsync(IReadOnlyCollection<BookCopyItem> input)
	{
		var pending = input.Where(x => string.IsNullOrWhiteSpace(x.AccessionNumber)).ToList();
		if (pending.Count == 0) return;
		var query = await bookCopyRepository.GetQueryableAsync();
		var existingNumbers = await AsyncExecuter.ToListAsync(query
			.Where(x => x.NormalizedAccessionNumber.StartsWith("ACC-"))
			.Select(x => x.NormalizedAccessionNumber));
		var reserved = existingNumbers.Concat(input.Where(item => !string.IsNullOrWhiteSpace(item.AccessionNumber))
			.Select(item => item.AccessionNumber!));
		var allocated = LibraryAccessionNumbers.Allocate(reserved, pending.Count);
		for (var index = 0; index < pending.Count; index++) pending[index].AccessionNumber = allocated[index];
	}
}
