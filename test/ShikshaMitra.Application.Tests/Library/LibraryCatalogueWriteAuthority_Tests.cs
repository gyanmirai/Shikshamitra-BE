using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueReadAuthority_Tests
{
	[Theory]
	[InlineData("Update")]
	[InlineData("Copies")]
	[InlineData("Delete")]
	public async Task Catalogue_Direct_Write_Should_Recheck_Revoked_Current_School_Scope(string surface)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		GrantWrite(surface);
		var opened = await GetRequiredService<IBookAppService>().GetAsync(context.BookId);
		await ReplaceAssignmentsAsync(null);
		var denied = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryAccessService>().GetContextAsync(context.YearId));
		denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);

		var error = await Should.ThrowAsync<BusinessException>(() => WriteAsync(surface, context, opened));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Theory]
	[InlineData("Update")]
	[InlineData("Copies")]
	[InlineData("Delete")]
	public async Task Catalogue_Direct_Write_Should_Use_Current_School_Context_Without_Requiring_The_Old_Origin_Year(string surface)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		GrantWrite(surface);
		var opened = await GetRequiredService<IBookAppService>().GetAsync(context.BookId);
		await ReplaceAssignmentsAsync(context.YearId);
		await GetRequiredService<ILibraryAccessService>().GetContextAsync(context.YearId);
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryAccessService>().GetContextAsync(context.OriginYearId));

		await Should.NotThrowAsync(() => WriteAsync(surface, context, opened));
	}

	private void GrantWrite(string surface) => GetRequiredService<IPermissionChecker>().IsGrantedAsync(surface == "Delete"
		? ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete : ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(true);

	private async Task WriteAsync(string surface, LibraryPolicyTestContext context, BookDetailDto opened)
	{
		var catalogue = GetRequiredService<IBookAppService>();
		switch (surface)
		{
			case "Update":
				await catalogue.UpdateAsync(context.BookId, new()
				{
					ExpectedMetadataRevision = opened.MetadataRevision,
					Title = "Reviewed catalogue title", Author = "School Author", Isbn = null,
					Publisher = "School Press", Edition = "1", Subject = "Reading", CategoryId = context.CategoryId,
					Language = Language.English, ApplicableAge = "10+", Pages = 100, Price = 100m, NumberOfCopies = 1
				});
				break;
			case "Copies":
				var copy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId));
				await catalogue.PutCopiesAsync(context.BookId, new() { ExpectedCopiesRevision = opened.CopiesRevision, Copies = [new()
				{
					CopyId = copy.Id, CopyNumber = 2, AccessionNumber = copy.AccessionNumber, SectionId = copy.SectionId, ShelfId = copy.ShelfId
				}] });
				break;
			case "Delete": await catalogue.DeleteAsync(context.BookId); break;
			default: throw new ArgumentOutOfRangeException(nameof(surface));
		}
	}
}
