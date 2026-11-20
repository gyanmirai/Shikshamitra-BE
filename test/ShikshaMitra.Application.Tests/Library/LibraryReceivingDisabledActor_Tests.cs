using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingDisabledActor_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Theory]
	[InlineData("Preview")]
	[InlineData("Receive")]
	[InlineData("Replay")]
	[InlineData("Get")]
	[InlineData("List")]
	public async Task Receiving_Should_Deny_A_Disabled_Persisted_Actor_With_Still_Granted_Actions_And_Scope(string operation)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = new LibraryReceivingInput { ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 1 }] };
		var app = GetRequiredService<ILibraryReceivingAppService>();
		var preview = await app.PreviewAsync(input); preview.CanReceive.ShouldBeTrue();
		var command = new LibraryReceiveCommand { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") };
		LibraryStockReceiptDto? original = null;
		if (operation is "Replay" or "Get" or "List") original = await app.ReceiveAsync(command);
		var originalReceipts = original == null ? 0 : 1;
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		(await WithUnitOfWorkAsync(() => GetRequiredService<IUserScopeService>().GetUserScopesAsync([actor])))[actor].HasAnyAccess.ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var identities = GetRequiredService<IIdentityUserRepository>();
			var identity = await identities.GetAsync(actor);
			identity.IsActive.ShouldBeTrue(); identity.SetIsActive(false);
			await identities.UpdateAsync(identity, true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IIdentityUserRepository>().GetAsync(actor)).IsActive.ShouldBeFalse();
			(await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().CountAsync(row => row.UserId == actor && row.IsActive)).ShouldBeGreaterThan(0);
		});
		var permissions = GetRequiredService<IPermissionChecker>();
		foreach (var action in new[] { ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.Receiving.Add, ShikshaMitraSubmodulePermissions.Library.AllBooks.View })
			(await permissions.IsGrantedAsync(action)).ShouldBeTrue();

		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			switch (operation)
			{
				case "Preview": await app.PreviewAsync(input); break;
				case "Receive": case "Replay": await app.ReceiveAsync(command); break;
				case "Get": await app.GetAsync(original!.Id); break;
				case "List": await app.GetListAsync(new()); break;
				default: throw new ArgumentOutOfRangeException(nameof(operation));
			}
		});

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IUserScopeService>().GetUserScopesAsync([actor])))[actor].HasAnyAccess.ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(row => row.BookId == context.BookId)).ShouldBe(1 + originalReceipts);
			(await GetRequiredService<IRepository<LibraryStockReceipt, Guid>>().CountAsync()).ShouldBe(originalReceipts);
			(await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync()).ShouldBe(originalReceipts);
		});
	}
}
