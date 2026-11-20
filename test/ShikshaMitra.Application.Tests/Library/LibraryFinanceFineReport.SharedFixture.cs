using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<string[]> RestrictFineReportReadAsync(Fixture owner, bool allowExport = false) => WithUnitOfWorkAsync(async () =>
	{
		var groups = await GetRequiredService<IPermissionDefinitionManager>().GetGroupsAsync();
		var allowed = new[] { ShikshaMitraSubmodulePermissions.Library.Reports.View,
			allowExport ? ShikshaMitraSubmodulePermissions.Library.Reports.Export : string.Empty };
		var denied = groups.SelectMany(group => group.GetPermissionsWithChildren()).Select(permission => permission.Name)
			.Where(name => (name.StartsWith("Library.", StringComparison.Ordinal) || name.StartsWith("Finance.", StringComparison.Ordinal)) &&
				!Enumerable.Contains(allowed, name)).Distinct().ToArray();
		var manager = GetRequiredService<IPermissionManager>();
		var role = $"external-finance-{owner.ActorId:N}";
		var current = await manager.GetAsync(denied, "R", role);
		foreach (var grant in current.Result.Where(grant => grant.IsGranted)) await manager.SetAsync(grant.Name, "R", role, false);
		await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.Reports.View, "R", role, true);
		await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.Reports.Export, "R", role, allowExport);
		return denied;
	});

	private Task AssertFineReportAuthorityAsync(Fixture owner, string[] denied, bool allowExport = false) => WithUnitOfWorkAsync(async () =>
	{
		var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Reports.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Reports.Export)).ShouldBe(allowExport);
		(await checker.IsGrantedAsync(denied)).Result.Values.ShouldNotContain(PermissionGrantResult.Granted);
		(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
	});

	private Task<FinanceExternalPositionQueryRow> FineReportPositionAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Reports,
			AssessmentIds: [owner.AssessmentId]));
		return await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query);
	});

	private static LibraryReportRequestDto FineReportRequest(Fixture owner) => new()
	{
		ReportType = LibraryReportTypes.FineCollection, Page = 1, PageSize = 50,
		Filters = new() { AcademicYearId = owner.YearId, MemberIds = [owner.PersonId] }
	};

	private FineReportTestServices FineReportServices(Fixture owner) =>
		new(new FinanceServices(ServiceProvider, owner, useStoredSource: true));

	private sealed class FineReportTestServices(IServiceProvider inner) : IServiceProvider
	{
		private readonly Dictionary<Type, object> instances = new();
		public LibraryReportAppService Reports => GetRequired<LibraryReportAppService>();
		public LibraryReportGenerator Generator => GetRequired<LibraryReportGenerator>();
		public LibraryReportScopeService Scope => GetRequired<LibraryReportScopeService>();
		public LibraryReportExportBackgroundJob Worker => GetRequired<LibraryReportExportBackgroundJob>();
		private T GetRequired<T>() where T : notnull => (T)GetService(typeof(T))!;
		public object? GetService(Type type)
		{
			if (type != typeof(ILibraryChargePositionReader) && type != typeof(LibraryReportGenerator) &&
				type != typeof(LibraryReportScopeService) && type != typeof(LibraryReportAppService) &&
				type != typeof(LibraryReportExportBackgroundJob)) return inner.GetService(type);
			if (instances.TryGetValue(type, out var existing)) return existing;
			var implementation = type == typeof(ILibraryChargePositionReader) ? typeof(LibraryChargePositionReader) : type;
			var instance = ActivatorUtilities.CreateInstance(this, implementation);
			if (instance is ApplicationService application) application.LazyServiceProvider = new AbpLazyServiceProvider(this);
			instances[type] = instance;
			return instance;
		}
	}
}
