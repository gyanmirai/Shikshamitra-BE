using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class StandardLookupFactory : AcademicSetupPagingFactory
{
	public UserScopeContext Scope { get; set; } = new() { HasWildcardAcademicYear = true };
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureServices(services => {
			var access = Substitute.For<IUserScopeAccessService>();
			access.GetCurrentUserScopeAsync().Returns(_ => Task.FromResult(Scope));
			services.Replace(ServiceDescriptor.Singleton(access));
		});
	}

	public async Task<Branch> SeedLookupBranchAsync(int count)
	{
		var branch = await SeedBranchAsync();
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var work = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var standards = services.GetRequiredService<IRepository<Standard, Guid>>();
		var divisions = services.GetRequiredService<IRepository<Division, Guid>>();
		var first = await standards.GetAsync(branch.StandardId);
		var roots = new List<Standard> { first };
		for (var index = 2; index <= count; index++)
		{
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(branch.StageId, $"Class {index}", 6, null);
			await standards.InsertAsync(standard, true);
			roots.Add(standard);
		}
		services.GetRequiredService<StandardManager>().Reorder(roots, roots.Select(row => row.Id).Reverse().ToArray());
		await standards.UpdateManyAsync(roots, true);
		var second = await services.GetRequiredService<DivisionManager>().CreateAsync(first.Id, "B", 40);
		await divisions.InsertAsync(second, true);
		var children = new[] { await divisions.GetAsync(branch.DivisionId), second };
		services.GetRequiredService<DivisionManager>().Reorder(children, children.Select(row => row.Id).Reverse().ToArray());
		await divisions.UpdateManyAsync(children, true);
		await work.CompleteAsync();
		return branch;
	}

	public async Task DeleteBoardAsync(Guid id)
	{
		using var scope = Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await scope.ServiceProvider.GetRequiredService<IRepository<EducationBoard, Guid>>().DeleteAsync(id, true);
		await work.CompleteAsync();
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public partial class StandardLookupApi_Tests(StandardLookupFactory factory) : IClassFixture<StandardLookupFactory>
{
	public sealed class Option
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = "";
		public List<Option> Divisions { get; set; } = [];
	}

	[Fact]
	public async Task Lookup_is_complete_across_years_and_matches_existing_academic_order_without_graph_payload()
	{
		using var client = factory.CreateClient();
		factory.Scope = new() { HasWildcardAcademicYear = true };
		var first = await factory.SeedLookupBranchAsync(12);
		var second = await factory.SeedLookupBranchAsync(1);
		var deleted = await factory.SeedLookupBranchAsync(1);
		await factory.DeleteBoardAsync(deleted.BoardId);
		var baseline = (await client.GetFromJsonAsync<PagedResultDto<Option>>("/api/app/standard?MaxResultCount=1000"))!;
		baseline.Items.Count.ShouldBeGreaterThan(10);
		factory.Commands.Clear();
		var result = (await client.GetFromJsonAsync<ListResultDto<Option>>("/api/app/standard/lookup"))!;
		AssertSame(result.Items, baseline.Items);
		result.Items.ShouldContain(row => row.Id == first.StandardId);
		result.Items.ShouldContain(row => row.Id == second.StandardId);
		result.Items.ShouldNotContain(row => row.Id == deleted.StandardId);
		result.Items.Single(row => row.Id == first.StandardId).Divisions.Select(row => row.Name).ShouldBe(["B", "A"]);
		factory.Commands.ShouldNotContain(sql => sql.Contains("\"Strength\"", StringComparison.Ordinal)
			|| sql.Contains("\"ShiftId\"", StringComparison.Ordinal) || sql.Contains("\"RoomNumber\"", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("standard")]
	[InlineData("division")]
	[InlineData("none")]
	public async Task Lookup_preserves_explicit_standard_only_and_descendant_only_scope(string mode)
	{
		using var client = factory.CreateClient();
		factory.Scope = new() { HasWildcardAcademicYear = true };
		var branch = await factory.SeedLookupBranchAsync(2);
		factory.Scope = mode switch {
			"standard" => new() { StandardIds = [branch.StandardId] },
			"division" => new() { DivisionIds = [branch.DivisionId] },
			_ => new()
		};
		var baseline = (await client.GetFromJsonAsync<PagedResultDto<Option>>("/api/app/standard?MaxResultCount=1000"))!;
		var result = (await client.GetFromJsonAsync<ListResultDto<Option>>("/api/app/standard/lookup"))!;
		AssertSame(result.Items, baseline.Items);
		if (mode == "none") result.Items.ShouldBeEmpty();
		else
		{
			result.Items.ShouldHaveSingleItem().Id.ShouldBe(branch.StandardId);
			if (mode == "standard") result.Items[0].Divisions.ShouldBeEmpty();
			else result.Items[0].Divisions.Select(row => row.Id).ShouldBe([branch.DivisionId]);
		}
	}

	[Fact]
	public async Task Lookup_keeps_existing_authenticated_list_policy_without_requiring_additional_actions()
	{
		using var client = factory.CreateClient();
		factory.Scope = new() { HasWildcardAcademicYear = true };
		await factory.SeedLookupBranchAsync(1);
		factory.Scope = new();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "No.Additional.Grant");
		var baseline = await client.GetAsync("/api/app/standard");
		baseline.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync("/api/app/standard/lookup")).StatusCode.ShouldBe(baseline.StatusCode);
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		baseline = await client.GetAsync("/api/app/standard");
		baseline.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await client.GetAsync("/api/app/standard/lookup")).StatusCode.ShouldBe(baseline.StatusCode);
	}

	private static void AssertSame(IReadOnlyList<Option> actual, IReadOnlyList<Option> expected)
	{
		actual.Select(row => (row.Id, row.Name)).ShouldBe(expected.Select(row => (row.Id, row.Name)));
		foreach (var pair in actual.Zip(expected))
			pair.First.Divisions.Select(row => (row.Id, row.Name)).ShouldBe(pair.Second.Divisions.Select(row => (row.Id, row.Name)));
	}
}
