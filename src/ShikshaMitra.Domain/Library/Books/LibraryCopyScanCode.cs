using System;
using System.Linq;

namespace ShikshaMitra.Library.Books;

public static class LibraryCopyScanCode
{
	private const string Prefix = "SM-LIB-COPY:";

	public static string For(BookCopy copy) => copy.AccessionNumber.Length > 0 &&
		copy.AccessionNumber.All(character => character >= ' ' && character <= '~')
		? copy.AccessionNumber
		: $"{Prefix}{copy.Id:N}";

	public static Guid? ReadCopyId(string value) => value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
		Guid.TryParseExact(value[Prefix.Length..], "N", out var id) ? id : null;
}
