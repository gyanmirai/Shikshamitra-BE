using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueReadAuthority_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Theory]
	[InlineData("Detail")]
	[InlineData("Copies")]
	[InlineData("Labels")]
	public async Task Catalogue_Direct_Read_Should_Recheck_Revoked_Current_School_Scope(string surface)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		await ReadAsync(surface, context.BookId);
		await ReplaceAssignmentsAsync(null);
		var denied = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryAccessService>().GetContextAsync(context.YearId));
		denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);

		var error = await Should.ThrowAsync<BusinessException>(() => ReadAsync(surface, context.BookId));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Theory]
	[InlineData("Detail")]
	[InlineData("Copies")]
	[InlineData("Labels")]
	public async Task Catalogue_Direct_Read_Should_Use_Current_School_Context_Instead_Of_A_Titles_Origin_Year(string surface)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		context.OriginYearId.ShouldNotBe(context.YearId);
		await ReplaceAssignmentsAsync(context.YearId);
		var access = GetRequiredService<ILibraryAccessService>();
		(await access.GetContextAsync(context.YearId)).AllowedAcademicYearIds.ShouldContain(context.YearId);
		await Should.ThrowAsync<BusinessException>(() => access.GetContextAsync(context.OriginYearId));

		await Should.NotThrowAsync(() => ReadAsync(surface, context.BookId));
	}

	private async Task ReplaceAssignmentsAsync(Guid? allowedYear)
	{
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == tenant && row.UserId == actor, true);
			if (allowedYear.HasValue)
				await assignments.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: allowedYear.Value,
					description: "Current school context only; old title origin year is not granted"), true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	}

	private async Task ReadAsync(string surface, Guid bookId)
	{
		var catalogue = GetRequiredService<IBookAppService>();
		switch (surface)
		{
			case "Detail": (await catalogue.GetAsync(bookId)).Id.ShouldBe(bookId); break;
			case "Copies": (await catalogue.GetCopiesAsync(bookId)).TotalCount.ShouldBe(1); break;
			case "Labels": (await catalogue.GetCopyLabelsAsync(bookId)).BookId.ShouldBe(bookId); break;
			default: throw new ArgumentOutOfRangeException(nameof(surface));
		}
	}
}
