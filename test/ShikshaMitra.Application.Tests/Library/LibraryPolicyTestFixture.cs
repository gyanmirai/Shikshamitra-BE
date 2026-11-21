using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library;

internal sealed record LibraryPolicyTestContext(Guid YearId, Guid OriginYearId, Guid CategoryId, Guid SectionId, Guid ShelfId, Guid BookId, Guid CopyId);

internal static class LibraryPolicyTestFixture
{
	public static async Task<LibraryPolicyTestContext> SeedAsync(IServiceProvider services)
	{
		await LibraryTestAuthorization.GrantTenantWideAsync(services);
		var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
		var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var yearId = Guid.NewGuid();
		var originYearId = Guid.NewGuid();
		await years.InsertAsync(new AcademicYear(yearId, tenantId, $"Library {Guid.NewGuid():N}"[..30],
			new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
		await years.InsertAsync(new AcademicYear(originYearId, tenantId, $"Library {Guid.NewGuid():N}"[..30],
			new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)), true);
		var category = new BookCategory($"Reading {Guid.NewGuid():N}", string.Empty, originYearId, tenantId);
		await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), tenantId, originYearId, "Main Library", 1);
		await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		var book = new Book("Persistent library title", $"{Guid.NewGuid():N}"[..13], "School Author", "School Press", "1",
			"Reading", category.Id, Language.English, "10+", 100m, 100, 1, originYearId, tenantId);
		await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, $"POL-{Guid.NewGuid():N}", section.Id, shelf.Id, tenantId);
		await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		await services.GetRequiredService<IRepository<BorrowingRule, Guid>>().InsertAsync(
			new BorrowingRule(Guid.NewGuid(), tenantId, yearId, 0, 4, 1), true);
		var timings = services.GetRequiredService<IRepository<LibraryTiming, Guid>>();
		foreach (var day in Enum.GetValues<DayOfWeek>())
			await timings.InsertAsync(new LibraryTiming(Guid.NewGuid(), day.ToString(), "09:00", "17:00",
				day is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1 : 0, yearId, tenantId), true);
		return new(yearId, originYearId, category.Id, section.Id, shelf.Id, book.Id, copy.Id);
	}
}
