using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueDeletionApi_Tests
{
	private HttpClient DeleteClient(bool granted) => ClientWithPermissions(granted
		? string.Join(',', ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)
		: ShikshaMitraSubmodulePermissions.Library.AllBooks.View);

	private HttpClient ClientWithPermissions(string grants)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants);
		return client;
	}

	private async Task<Guid> SeedUnusedTitleAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory($"Delete {Guid.NewGuid():N}", "", Factory.SeededAcademicYearId, tenantId), true);
		var section = await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
			new(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId, "Reading", 1), true);
		var shelf = await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(new(Guid.NewGuid(), section.Id, "A1"), true);
		var book = await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(new("Unused catalogue title", "", "Known author",
			"", "", "", category.Id, Language.English, "", 0m, 0, 1, Factory.SeededAcademicYearId, tenantId), true);
		await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(new(Guid.NewGuid(), book.Id, 1,
			$"DEL-{Guid.NewGuid():N}", section.Id, shelf.Id, tenantId), true);
		await unit.CompleteAsync();
		return book.Id;
	}
}
