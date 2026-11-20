using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryApiTestCollection.Name)]
public class LibraryAccessionRoutingApi_Tests(LibraryApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Query_Routes_Should_Preserve_Local_Language_Accession_And_Url_Punctuation()
	{
		var (bookId, copyId, accession) = await SeedCopyAsync();
		var encoded = Uri.EscapeDataString(accession);
		var found = await GetAndReadAsync<AccessionBookDto>($"/api/library/books/accession?accessionNo={encoded}");
		found.AccessionNo.ShouldBe(accession);

		var member = await Client.GetAsync($"/api/library/issuedBook/member?accessionNo={encoded}");
		member.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await member.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await member.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryErrorCodes.ActiveIssueNotFound);

		var state = await GetAndReadAsync<LibraryCopyStateDto>($"/api/library/books/{bookId}/copies/{copyId}/state");
		var changed = await Client.PutAsJsonAsync($"/api/library/books/{bookId}/copies/status?accessionNumber={encoded}",
			new UpdateBookCopyStatusRequest { Status = BookCopyStatus.Damaged, Remarks = "Cover torn", Reason = "Cover damage confirmed", Revision = state.Revision }, JsonOptions);
		changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
		var copies = await GetAndReadAsync<PaginatedResponse<BookCopyDto>>($"/api/library/books/{bookId}/copies");
		copies.Items.ShouldContain(x => x.AccessionNumber == accession && x.Status == nameof(BookCopyStatus.Damaged));
	}

	[Fact]
	public async Task New_Query_Lookup_Should_Retain_The_Existing_View_Permission_Check()
	{
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		var response = await denied.GetAsync("/api/library/books/accession?accessionNo=anything");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Catalogue_Editor_Should_Provide_Real_Options_Without_Unrelated_Settings_Grants()
	{
		await SeedCopyAsync();
		using var catalogue = Factory.CreateClient();
		catalogue.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		catalogue.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"{ShikshaMitraSubmodulePermissions.Library.AllBooks.View},{ShikshaMitraSubmodulePermissions.Library.AllBooks.Add}");

		var response = await catalogue.GetAsync($"/api/library/books/editor-options?academicYearId={Factory.SeededAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var options = await response.Content.ReadFromJsonAsync<LibraryBookEditorOptionsDto>(JsonOptions);
		options.ShouldNotBeNull();
		options.BookCategories.ShouldContain(row => row.Name.StartsWith("Routing test", StringComparison.Ordinal));
		options.Sections.ShouldContain(row => row.SectionName.StartsWith("Routing test", StringComparison.Ordinal) &&
			row.ShelfDetails.Exists(shelf => shelf.ShelfName == "A"));
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add)]
	public async Task Catalogue_Editor_Should_Require_Both_View_And_An_Editing_Grant(string onlyPermission)
	{
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, onlyPermission);

		var response = await denied.GetAsync($"/api/library/books/editor-options?academicYearId={Factory.SeededAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private async Task<(Guid BookId, Guid CopyId, string Accession)> SeedCopyAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenantContext = services.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var fixtureName = $"Routing test {Guid.NewGuid():N}"[..25];
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory(fixtureName, "Routing evidence", Factory.SeededAcademicYearId, tenant), true);
		var section = await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
			new LibrarySection(Guid.NewGuid(), tenant, Factory.SeededAcademicYearId, fixtureName, 1), true);
		var shelf = await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(
			new LibraryShelf(Guid.NewGuid(), section.Id, "A"), true);
		var book = await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(
			new Book("વાર્તાઓ", "", "Asha", "", "", "", category.Id, Language.English, "", 0m, 0, 1,
				Factory.SeededAcademicYearId, tenant), true);
		var accession = $"પુસ્તક/26 #1?A+B-{Guid.NewGuid().ToString("N")[..8]}";
		var copy = await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
			new BookCopy(Guid.NewGuid(), book.Id, 1, accession, section.Id, shelf.Id, tenant), true);
		await uow.CompleteAsync();
		return (book.Id, copy.Id, accession);
	}
}
