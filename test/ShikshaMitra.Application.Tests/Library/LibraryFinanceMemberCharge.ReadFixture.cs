using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<string[]> RestrictMemberChargeReadAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var groups = await GetRequiredService<IPermissionDefinitionManager>().GetGroupsAsync();
		var denied = groups.SelectMany(group => group.GetPermissionsWithChildren()).Select(permission => permission.Name)
			.Where(name => (name.StartsWith("Library.", StringComparison.Ordinal) || name.StartsWith("Finance.", StringComparison.Ordinal)) &&
				name != ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View).Distinct().ToArray();
		var manager = GetRequiredService<IPermissionManager>();
		var role = $"external-finance-{owner.ActorId:N}";
		var current = await manager.GetAsync(denied, "R", role);
		foreach (var grant in current.Result.Where(grant => grant.IsGranted)) await manager.SetAsync(grant.Name, "R", role, false);
		await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View, "R", role, true);
		return denied;
	});

	private Task AssertMemberChargeReadAuthorityAsync(Fixture owner, string[] denied) => WithUnitOfWorkAsync(async () =>
	{
		var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(denied)).Result.Values.ShouldNotContain(PermissionGrantResult.Granted);
		(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
	});

	private Task AssertMemberChargeTargetAsync(Fixture owner, Guid? request = null) => WithUnitOfWorkAsync(async () =>
	{
		var ids = await GetRequiredService<ILibraryAccessService>().GetPersonMemberIdsAsync(request ?? owner.PersonId);
		ids.ShouldContain(owner.PersonId);
	});

	private Task<FinanceExternalPositionQueryRow> MemberChargePositionAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.MemberCharges,
			AssessmentIds: [owner.AssessmentId]));
		return await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query);
	});

	private LibraryMemberAppService MemberChargeReadService(Fixture owner, IFinanceExternalPositionReader? detailOverride = null)
	{
		var services = new MemberChargeReadServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true), detailOverride);
		var service = ActivatorUtilities.CreateInstance<LibraryMemberAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private sealed class MemberChargeReadServices(IServiceProvider inner, IFinanceExternalPositionReader? details) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(IFinanceExternalPositionReader) && details != null ? details
			: type == typeof(ILibraryChargePositionReader) ? ActivatorUtilities.CreateInstance<LibraryChargePositionReader>(this)
			: inner.GetService(type);
	}

	private static JsonElement MemberChargeWire(MemberChargeItemDto item) =>
		JsonSerializer.SerializeToElement(item, new JsonSerializerOptions(JsonSerializerDefaults.Web));

	private static JsonElement RequireMemberChargePosition(MemberChargeItemDto item)
	{
		var json = MemberChargeWire(item);
		json.TryGetProperty("financePosition", out var position).ShouldBeTrue("Expose the authorized canonical position separately from local declarations.");
		position.ValueKind.ShouldBe(JsonValueKind.Object, "A real returned row must be mapped; absence must not fabricate Unposted.");
		return position;
	}

	private static void AssertNullableMemberChargePositionDefault()
	{
		var json = MemberChargeWire(new MemberChargeItemDto());
		if (json.TryGetProperty("financePosition", out var position)) position.ValueKind.ShouldBe(JsonValueKind.Null);
	}
}
