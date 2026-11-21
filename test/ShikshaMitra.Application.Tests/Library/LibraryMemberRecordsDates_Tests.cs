using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData("borrowed")]
	[InlineData("history")]
	[InlineData("reserved")]
	public async Task H_Member_Records_Should_Return_Canonical_Source_Dates_And_Keep_Raw_Compatibility_Values_Unchanged(string kind)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff: false);
		var requestId = await SeedStatisticsRequestAsync(fixture, fixture.Context.StudentId);
		var app = GetRequiredService<ILibraryMemberAppService>();
		if (kind == "reserved")
		{
			var row = (await app.GetReservedBooksAsync(fixture.Context.StudentId)).Items.Single();
			row.Id.ShouldBe(requestId);
			row.RequestedDate.ShouldBe(new DateOnly(2026, 11, 17));
			row.RequestedDateProvenance.ShouldBe(1);
			row.ReservedOn.ShouldBeNull();
			row.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
			row.State.ShouldBe(0);
		}
		else if (kind == "borrowed")
		{
			var row = (await app.GetBorrowedBooksAsync(fixture.Context.StudentId)).Items.Single();
			row.Id.ShouldBe(loan.Id);
			row.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 17)); row.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
			row.IssueDateProvenance.ShouldBe(1); row.DueDateProvenance.ShouldBe(1);
			row.IssuedOn.ShouldBeNull(); row.DueOn.ShouldBeNull();
			row.CopyId.ShouldBe(fixture.Context.Catalog.CopyId); row.BookId.ShouldBe(fixture.Context.Catalog.BookId);
			row.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		}
		else
		{
			var row = (await app.GetBorrowHistoryAsync(fixture.Context.StudentId)).Items.Single();
			row.BorrowId.ShouldBe(loan.Id);
			row.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 17)); row.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
			row.IssueDateProvenance.ShouldBe(1); row.DueDateProvenance.ShouldBe(1);
			row.IssuedOn.ShouldBeNull(); row.DueOn.ShouldBeNull();
			row.CopyId.ShouldBe(fixture.Context.Catalog.CopyId); row.BookId.ShouldBe(fixture.Context.Catalog.BookId);
			row.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_Member_Records_Current_Profile_Should_Use_The_Same_Confirmed_Statistics_Projection(bool staff)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		await CheckoutStatisticsMemberAsync(fixture, staff);
		var now = new DateTime(2026, 11, 25, 0, 30, 0, DateTimeKind.Utc);
		var expected = await ReadStatisticsAsync(memberId, "UTC", now);
		expected.OverdueReturnBooks.ShouldBe(1);
		expected.OverdueClassificationAvailable.ShouldBeTrue();
		var services = new StatisticsClockServices(ServiceProvider, now);

		var member = await WithUnitOfWorkAsync(() => services.GetMemberService().GetMemberAsync(memberId));

		member.Id.ShouldBe(memberId);
		member.Statistics.CurrentIssue.ShouldBe(expected.CurrentIssue);
		member.Statistics.OverdueReturnBooks.ShouldBe(expected.OverdueReturnBooks);
		member.Statistics.OverdueClassificationAvailable.ShouldBeTrue();
		member.Statistics.OverdueAsOfDate.ShouldBe(expected.OverdueAsOfDate);
	}

	[Fact]
	public async Task H_Member_Records_Borrowed_Title_Should_Follow_The_Saved_Copy_Id_Rather_Than_A_Conflicting_Accession()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff: false);
		await WithUnitOfWorkAsync(async () =>
		{
			var catalog = fixture.Context.Catalog;
			var decoy = new Book("Conflicting accession title", "", "Author", "Publisher", "1", "Reading", catalog.CategoryId,
				Language.English, "10+", 100m, 100, 1, catalog.OriginYearId, GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id);
			await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(decoy, true);
			var copy = new BookCopy(Guid.NewGuid(), decoy.Id, 1, $"DECOY-{Guid.NewGuid():N}", catalog.SectionId, catalog.ShelfId, decoy.TenantId);
			await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
			var saved = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id);
			saved.AccessionNumber = copy.AccessionNumber;
			await GetRequiredService<IRepository<BookIssue, Guid>>().UpdateAsync(saved, true);
		});

		var row = (await GetRequiredService<ILibraryMemberAppService>().GetBorrowedBooksAsync(fixture.Context.StudentId)).Items.Single();

		row.BookTitle.ShouldBe("Persistent library title");
	}
}
