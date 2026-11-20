using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCatalogIntegrity_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IBookAppService books;
	private readonly IRepository<Book, Guid> bookRepository;
	private readonly IRepository<BookCopy, Guid> copies;
	private readonly IRepository<BookIssue, Guid> issues;
	private readonly ICurrentTenant tenant;

	public LibraryCatalogIntegrity_Tests()
	{
		books = GetRequiredService<IBookAppService>();
		bookRepository = GetRequiredService<IRepository<Book, Guid>>();
		copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		issues = GetRequiredService<IRepository<BookIssue, Guid>>();
		tenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Copy_Status_Update_Should_Reject_Another_Tenants_Book()
	{
		BookCopy copy;
		var ownerTenant = Guid.NewGuid();
		using (tenant.Change(ownerTenant))
		using (ChangePrincipal(ownerTenant)) copy = await CreateCopyAsync();
		using (tenant.Change(Guid.NewGuid()))
		{
			await Should.ThrowAsync<EntityNotFoundException>(() => books.UpdateCopyStatusAsync(
				copy.BookId, copy.AccessionNumber, new UpdateBookCopyStatusRequest { Status = BookCopyStatus.Damaged }));
		}
	}

	[Fact]
	public async Task Put_Copies_Should_Preserve_Returned_Copy_Identity_Condition_And_Remarks()
	{
		var copy = await CreateCopyAsync();
		var issueId = await RecordReturnedLoanAsync(copy);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await copies.GetAsync(copy.Id);
			stored.Status = BookCopyStatus.Damaged;
			stored.Remarks = "Spine needs repair";
			await copies.UpdateAsync(stored, true);
		});

		await books.PutCopiesAsync(copy.BookId, await PayloadAsync(copy));

		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await copies.FindAsync(copy.Id);
			stored.ShouldNotBeNull();
			stored.Status.ShouldBe(BookCopyStatus.Damaged);
			stored.Remarks.ShouldBe("Spine needs repair");
			(await issues.GetAsync(issueId)).BookCopyId.ShouldBe(stored.Id);
		});
	}

	[Fact]
	public async Task Put_Copies_Should_Allow_Metadata_Edit_While_Copy_Is_On_Loan()
	{
		var copy = await CreateCopyAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			await issues.InsertAsync(new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber,
				Guid.NewGuid(), 0, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7), tenant.Id), true);
			var stored = await copies.GetAsync(copy.Id);
			stored.Status = BookCopyStatus.Issued;
			await copies.UpdateAsync(stored, true);
		});
		var payload = await PayloadAsync(copy);
		payload.Copies[0].CopyNumber = 7;

		await books.PutCopiesAsync(copy.BookId, payload);

		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await copies.GetAsync(copy.Id);
			stored.CopyNumber.ShouldBe(7);
			stored.Status.ShouldBe(BookCopyStatus.Issued);
		});
	}

	[Fact]
	public async Task Put_Copies_Should_Reject_Omitting_Copy_With_Returned_Loan_History()
	{
		var copy = await CreateCopyAsync();
		await RecordReturnedLoanAsync(copy);
		var replacement = await PayloadAsync(copy);
		replacement.Copies[0].AccessionNumber = $"NEW-{Guid.NewGuid():N}";

		var error = await Should.ThrowAsync<BusinessException>(() => books.PutCopiesAsync(copy.BookId, replacement));
		error.Message.ShouldContain("retire", Case.Insensitive);
		await WithUnitOfWorkAsync(async () => (await copies.FindAsync(copy.Id)).ShouldNotBeNull());
	}

	[Fact]
	public async Task Put_Copies_Should_Reject_Changing_Accession_Of_Identified_Copy()
	{
		var copy = await CreateCopyAsync();
		var payload = JsonSerializer.Deserialize<PutBookCopiesRequest>(JsonSerializer.Serialize(new
		{
			Copies = new[] { new { CopyId = copy.Id, copy.CopyNumber, AccessionNumber = "CHANGED",
				copy.SectionId, copy.ShelfId } }
		}))!;
		payload.ExpectedCopiesRevision = (await books.GetAsync(copy.BookId)).CopiesRevision;

		var error = await Should.ThrowAsync<BusinessException>(() => books.PutCopiesAsync(copy.BookId, payload));
		error.Message.ShouldContain("accession", Case.Insensitive);
		await WithUnitOfWorkAsync(async () => (await copies.GetAsync(copy.Id)).AccessionNumber.ShouldBe(copy.AccessionNumber));
	}

	[Fact]
	public async Task Put_Copies_Should_Reject_Unknown_Copy_Identity_Even_When_Accession_Matches()
	{
		var copy = await CreateCopyAsync();
		var payload = JsonSerializer.Deserialize<PutBookCopiesRequest>(JsonSerializer.Serialize(new
		{
			Copies = new[] { new { CopyId = Guid.NewGuid(), copy.CopyNumber, copy.AccessionNumber,
				copy.SectionId, copy.ShelfId } }
		}))!;
		payload.ExpectedCopiesRevision = (await books.GetAsync(copy.BookId)).CopiesRevision;

		await Should.ThrowAsync<BusinessException>(() => books.PutCopiesAsync(copy.BookId, payload));
		await WithUnitOfWorkAsync(async () => (await copies.FindAsync(copy.Id)).ShouldNotBeNull());
	}

	[Fact]
	public async Task Delete_Should_Keep_Book_And_Copy_With_Returned_Loan_History()
	{
		var copy = await CreateCopyAsync();
		await RecordReturnedLoanAsync(copy);

		await Should.ThrowAsync<BusinessException>(() => books.DeleteAsync(copy.BookId));

		await WithUnitOfWorkAsync(async () =>
		{
			(await copies.FindAsync(copy.Id)).ShouldNotBeNull();
			(await bookRepository.FindAsync(copy.BookId)).ShouldNotBeNull();
		});
	}

	[Fact]
	public async Task Different_Tenants_Should_Be_Able_To_Use_The_Same_Accession()
	{
		var accession = $"SHARED-{Guid.NewGuid():N}";
		foreach (var tenantId in new[] { Guid.NewGuid(), Guid.NewGuid() })
		{
			using (tenant.Change(tenantId))
			using (ChangePrincipal(tenantId))
			{
				var context = await ContextAsync();
				var request = Request(context, Guid.NewGuid().ToString("N")[..13]);
				request.Copies[0].AccessionNumber = accession;
				var created = await books.CreateAsync(context.YearId, request);
				(await books.GetCopiesAsync(Guid.Parse(created.Id))).TotalCount.ShouldBe(1);
			}
		}
	}

	[Fact]
	public async Task Create_Should_Reject_Accession_Used_By_Another_Title_Ignoring_Case()
	{
		var context = await ContextAsync();
		var first = Request(context, Guid.NewGuid().ToString("N")[..13]);
		var second = Request(context, Guid.NewGuid().ToString("N")[..13]);
		second.Copies[0].AccessionNumber = first.Copies[0].AccessionNumber!.ToLowerInvariant();
		await books.CreateAsync(context.YearId, first);

		var error = await Should.ThrowAsync<BusinessException>(() => books.CreateAsync(context.YearId, second));
		error.Code.ShouldBe(LibraryErrorCodes.AccessionNumberAlreadyExists);
	}

	[Fact]
	public async Task List_Should_Count_Actual_Copies_Instead_Of_Legacy_Declared_Stock()
	{
		var copy = await CreateCopyAsync();
		var yearId = await WithUnitOfWorkAsync(async () =>
		{
			var book = await bookRepository.GetAsync(copy.BookId);
			book.NumberOfCopies = 99;
			await bookRepository.UpdateAsync(book, true);
			return book.AcademicYearId!.Value;
		});

		var result = await books.GetListAsync(yearId);

		result.Items.Single().NumberOfCopies.ShouldBe(1);
	}

	[Fact]
	public async Task Create_Should_Allow_Multiple_Different_Titles_Without_Isbn()
	{
		var context = await ContextAsync();
		var first = Request(context, string.Empty);
		var second = Request(context, string.Empty);
		first.Edition = second.Edition = string.Empty;

		await books.CreateAsync(context.YearId, first);
		await books.CreateAsync(context.YearId, second);

		(await books.GetListAsync(context.YearId)).TotalCount.ShouldBe(2);
	}

	[Fact]
	public async Task Create_Should_Reject_Equivalent_Isbn_With_Different_Separators()
	{
		var context = await ContextAsync();
		var first = Request(context, "978-0-306-40615-7");
		var second = Request(context, "978 0 306 40615 7");
		first.Edition = second.Edition = "1";
		await books.CreateAsync(context.YearId, first);

		var error = await Should.ThrowAsync<BusinessException>(() => books.CreateAsync(context.YearId, second));
		error.Code.ShouldBe(LibraryErrorCodes.BookAlreadyExists);
	}

	[Fact]
	public async Task Create_Should_Generate_Different_Accessions_For_Blank_Copy_Inputs()
	{
		var context = await ContextAsync();
		var request = Request(context, Guid.NewGuid().ToString("N")[..13]);
		request.Copies[0].AccessionNumber = string.Empty;
		request.Copies.Add(new BookCopyItem
		{
			CopyNumber = 2, AccessionNumber = string.Empty, SectionId = context.SectionId, ShelfId = context.ShelfId
		});
		var created = await books.CreateAsync(context.YearId, request);
		var stored = await books.GetCopiesAsync(Guid.Parse(created.Id));

		stored.Items.Count().ShouldBe(2);
		stored.Items.All(x => !string.IsNullOrWhiteSpace(x.AccessionNumber)).ShouldBeTrue();
		stored.Items.Select(x => x.AccessionNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(2);
	}

	[Fact]
	public async Task Create_Should_Count_Only_Persisted_Copies()
	{
		var context = await ContextAsync();
		var request = Request(context, Guid.NewGuid().ToString("N")[..13]);
		request.Copies.Clear();
		request.NumberOfCopies = 5;
		var created = await books.CreateAsync(context.YearId, request);

		await WithUnitOfWorkAsync(async () =>
			(await bookRepository.GetAsync(Guid.Parse(created.Id))).NumberOfCopies.ShouldBe(0));
	}

	private async Task<BookCopy> CreateCopyAsync()
	{
		var context = await ContextAsync();
		var created = await books.CreateAsync(context.YearId, Request(context, Guid.NewGuid().ToString("N")[..13]));
		return await WithUnitOfWorkAsync(async () =>
			(await copies.GetListAsync(x => x.BookId == Guid.Parse(created.Id))).Single());
	}

	private async Task<Guid> RecordReturnedLoanAsync(BookCopy copy)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var issue = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, Guid.NewGuid(), 0,
				DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date.AddDays(-3), tenant.Id)
			{
				ReturnDate = DateTime.UtcNow.Date.AddDays(-2), Status = BookIssueStatus.Returned
			};
			await issues.InsertAsync(issue, true);
			return issue.Id;
		});
	}

	private async Task<CatalogContext> ContextAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			await LibraryTestAuthorization.GrantTenantWideAsync(ServiceProvider);
			var year = Guid.NewGuid();
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(
				year, tenant.Id, $"Catalog {Guid.NewGuid():N}"[..30], new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var category = new BookCategory($"Catalog {Guid.NewGuid():N}", string.Empty, year, tenant.Id);
			await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
			var section = new LibrarySection(Guid.NewGuid(), tenant.Id, year, "Reading", 1);
			await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
			var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
			await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
			return new CatalogContext(year, category.Id, section.Id, shelf.Id);
		});
	}

	private IDisposable ChangePrincipal(Guid tenantId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
		new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
			new Claim(AbpClaimTypes.UserName, "library-test-user"),
			new Claim(AbpClaimTypes.TenantId, tenantId.ToString())
		], "LibraryTest")));

	private static CreateBookRequest Request(CatalogContext context, string isbn) => new()
	{
		Title = $"Book {Guid.NewGuid():N}", Isbn = isbn, Author = "Test Author", Edition = "1", Language = Language.English,
		Publisher = "School Press", Subject = "Reading", ApplicableAge = "10+",
		CategoryId = context.CategoryId, NumberOfCopies = 1,
		Copies = [new BookCopyItem
		{
			CopyNumber = 1, AccessionNumber = $"TST-{Guid.NewGuid():N}",
			SectionId = context.SectionId, ShelfId = context.ShelfId
		}]
	};

	private async Task<PutBookCopiesRequest> PayloadAsync(BookCopy copy) => new()
	{
		ExpectedCopiesRevision = (await books.GetAsync(copy.BookId)).CopiesRevision,
		Copies = [new BookCopyItem
		{
			CopyNumber = copy.CopyNumber, AccessionNumber = copy.AccessionNumber,
			SectionId = copy.SectionId, ShelfId = copy.ShelfId
		}]
	};

	private sealed record CatalogContext(Guid YearId, Guid CategoryId, Guid SectionId, Guid ShelfId);
}
