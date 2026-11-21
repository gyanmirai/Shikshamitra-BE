using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

public partial class BookAppService
{
	private const string CopyRemovalBlockedReason =
		"Copies with receiving provenance, circulation history or a recorded condition cannot be removed. Keep the copies and retire them through copy status instead.";

	private async Task<string?> TitleRemovalBlockedReasonAsync(Guid bookId, IReadOnlyDictionary<Guid, LibraryCopyRetention> retention)
	{
		if (retention.Values.Any(facts => facts.BlockedReason != null)) return CopyRemovalBlockedReason;
		return await reservationRepository.AnyAsync(reservation => reservation.BookId == bookId)
			? "Books with reservation history cannot be deleted; retire their copies instead." : null;
	}
}
