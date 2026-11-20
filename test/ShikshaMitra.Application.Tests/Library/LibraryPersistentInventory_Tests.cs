using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPersistentInventory_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Current_Context_Dashboard_Stock_Should_Include_Old_Holdings_And_Their_Persistent_Locations()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedCurrentContextAsync();

		var overview = await GetRequiredService<ILibraryDashboardAppService>().GetOverviewAsync(context.YearId);

		overview.StockTotals.TotalBooks.ShouldBe(1);
		overview.StockTotals.TotalCopies.ShouldBe(1);
		overview.StockTotals.DamagedCopies.ShouldBe(1);
		overview.CategoryDistribution.Single().CategoryId.ShouldBe(context.CategoryId);
		overview.CategoryDistribution.Single().CopyCount.ShouldBe(1);
		overview.SectionInventory.Single().SectionId.ShouldBe(context.SectionId);
		overview.SectionInventory.Single().CopyCount.ShouldBe(1);
		await AssertOriginalHoldingsAsync(context);
	}

	[Theory]
	[InlineData(LibraryReportTypes.BookMaster)]
	[InlineData(LibraryReportTypes.StockSummary)]
	[InlineData(LibraryReportTypes.AvailabilityStatus)]
	[InlineData(LibraryReportTypes.LostDamagedBooks)]
	public async Task Current_Context_Inventory_Report_Should_Include_Old_Holdings_Without_Cloning(string reportType)
	{
		var context = await SeedCurrentContextAsync();

		var preview = await GetRequiredService<ILibraryReportAppService>().PreviewAsync(new()
		{
			ReportType = reportType,
			Filters = new() { AcademicYearId = context.YearId }
		});

		preview.TotalCount.ShouldBe(1);
		var row = preview.Rows.Single();
		if (reportType == LibraryReportTypes.StockSummary) row["totalCopies"].ShouldBe("1");
		else row["title"].ShouldBe("Persistent library title");
		if (reportType == LibraryReportTypes.BookMaster) row["bookId"].ShouldBe(context.BookId.ToString());
		if (reportType is LibraryReportTypes.AvailabilityStatus or LibraryReportTypes.LostDamagedBooks)
			row["status"].ShouldBe(nameof(BookCopyStatus.Damaged));
		await AssertOriginalHoldingsAsync(context);
	}

	[Fact]
	public async Task Report_Options_Should_Include_Persistent_Categories_And_Sections_Outside_The_Authorized_Origin_Year()
	{
		var context = await SeedCurrentContextAsync();

		var options = await GetRequiredService<ILibraryReportAppService>().GetOptionsAsync();

		options.Categories.Select(x => x.Id).ShouldContain(context.CategoryId.ToString());
		options.Sections.Select(x => x.Id).ShouldContain(context.SectionId.ToString());
		options.AcademicYears.Select(x => x.Id).ShouldContain(context.YearId.ToString());
		options.AcademicYears.Select(x => x.Id).ShouldNotContain(context.OriginYearId.ToString());
	}

	private Task<LibraryPolicyTestContext> SeedCurrentContextAsync() => WithUnitOfWorkAsync(async () =>
	{
		var context = await LibraryPolicyTestFixture.SeedAsync(ServiceProvider);
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(x => x.UserId == userId, true);
		await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId,
			academicYearId: context.YearId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var copy = await copies.GetAsync(context.CopyId);
		copy.Status = BookCopyStatus.Damaged;
		await copies.UpdateAsync(copy, true);
		return context;
	});

	private Task AssertOriginalHoldingsAsync(LibraryPolicyTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		var books = await GetRequiredService<IRepository<Book, Guid>>().GetListAsync();
		books.Single().Id.ShouldBe(context.BookId);
		books.Single().AcademicYearId.ShouldBe(context.OriginYearId);
		(await GetRequiredService<IRepository<BookCopy, Guid>>().GetListAsync()).Single().Id.ShouldBe(context.CopyId);
	});
}
