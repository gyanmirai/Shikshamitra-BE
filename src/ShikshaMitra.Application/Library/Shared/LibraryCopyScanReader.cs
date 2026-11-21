using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Shared;

public sealed class LibraryCopyScanReader(IRepository<BookCopy, Guid> copies, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<BookCopy?> FindAsync(Guid? tenantId, string value, IQueryable<Guid>? allowedSections = null)
	{
		var normalized = value.ToUpperInvariant();
		var tokenId = LibraryCopyScanCode.ReadCopyId(value) ?? Guid.Empty;
		var query = (await copies.GetQueryableAsync()).Where(row => row.TenantId == tenantId &&
			(row.NormalizedAccessionNumber == normalized || tokenId != Guid.Empty && row.Id == tokenId));
		if (allowedSections != null) query = query.Where(row => allowedSections.Contains(row.SectionId));
		var rows = await queries.ToListAsync(query.Take(2));
		if (rows.Count > 1) throw new BusinessException(LibraryErrorCodes.DuplicateAccessionNumber,
			"This scan identifies more than one inventory copy. Review the conflicting identifiers.");
		var copy = rows.SingleOrDefault();
		return copy != null && (string.Equals(copy.AccessionNumber, value, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(LibraryCopyScanCode.For(copy), value, StringComparison.OrdinalIgnoreCase)) ? copy : null;
	}
}
