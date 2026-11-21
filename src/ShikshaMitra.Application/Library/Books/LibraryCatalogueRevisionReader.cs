using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryCatalogueRevisionReader(IRepository<BookCopy, Guid> copies, LibraryCopyRetentionReader retention)
	: ITransientDependency
{
	internal static string Metadata(Book book) => Fingerprint(new
	{
		book.TenantId, book.Id, book.Title, book.Isbn, book.Author, book.Publisher, book.Edition,
		book.Subject, book.CategoryId, book.Language, book.LanguageCode, book.MaterialCode,
		book.ApplicableAge, Price = LibraryCataloguePriceRules.Format(book.Price), book.Pages, book.CoverImage
	});

	internal static void RequireMetadata(Book book, string? expected) => Require(expected, Metadata(book));

	internal async Task<string> CopiesAsync(Book book)
	{
		var rows = await copies.GetListAsync(copy => copy.BookId == book.Id);
		var facts = await retention.ReadAsync(rows);
		return Copies(book, rows, facts);
	}

	internal static string Copies(Book book, IReadOnlyCollection<BookCopy> rows, IReadOnlyDictionary<Guid, LibraryCopyRetention> facts)
	{
		return Fingerprint(new
		{
			book.TenantId, BookId = book.Id,
			Copies = rows.OrderBy(copy => copy.Id).Select(copy => new
			{
				copy.Id, copy.CopyNumber, copy.AccessionNumber, copy.NormalizedAccessionNumber,
				copy.SectionId, copy.ShelfId, copy.Status, copy.Remarks,
				facts[copy.Id].ReceiptId, facts[copy.Id].BlockedReason
			})
		});
	}

	internal async Task RequireCopiesAsync(Book book, string? expected) => Require(expected, await CopiesAsync(book));

	private static string Fingerprint(object value) => "v1:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

	private static void Require(string? expected, string actual)
	{
		if (!string.Equals(expected, actual, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.CatalogueRevisionChanged,
				"The catalogue record or copies changed. Keep your draft and review the current record before saving.");
	}
}
