using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Net.Http.Json;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings;

public class AcademicSetupPagingFactory : AcademicSetupValidationFactory
{
	public ConcurrentQueue<string> Commands { get; } = new();
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureServices(services => services.Configure<AbpDbContextOptions>(options => options.PreConfigure(context =>
			context.DbContextOptions.AddInterceptors(new AcademicReadRecorder(Commands)))));
	}
	private sealed class AcademicReadRecorder(ConcurrentQueue<string> commands) : DbCommandInterceptor
	{
		public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
		{ commands.Enqueue(command.CommandText); return result; }
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{ commands.Enqueue(command.CommandText); return ValueTask.FromResult(result); }
	}
	public record Branch(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid StandardId, Guid DivisionId);

	public async Task<Branch> SeedBranchAsync()
	{
		var stageId = await SeedStageAsync();
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var stage = await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 1", 6, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 40);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var result = new Branch(stage.EducationMedium.EducationBoard.AcademicYearId,
			stage.EducationMedium.EducationBoardId, stage.EducationMediumId, stage.Id, standard.Id, division.Id);
		await unit.CompleteAsync();
		return result;
	}

	public async Task SetScopeAsync(Branch? branch)
	{
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var assignments = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(x => x.UserId == userId, autoSave: true);
		await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId,
			academicYearId: branch?.YearId, divisionId: branch?.DivisionId), autoSave: true);
		await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		await unit.CompleteAsync();
	}
	public async Task<Guid> SeedRoomAsync(string name, bool foreign = false)
	{
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(foreign ? Guid.NewGuid() : Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var building = await services.GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building { BuildingName = "Reference test", TotalFloors = 1 }, true);
		var floor = await services.GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor { BuildingId = building.Id, FloorName = "Ground", TotalRoom = 10 }, true);
		var type = await services.GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(new RoomType { RoomTypeName = "Classroom", ShortName = "CR" }, true);
		var room = await services.GetRequiredService<IRepository<Room, Guid>>().InsertAsync(new Room {
			RoomName = name, BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id, Capacity = 20 }, true);
		await unit.CompleteAsync();
		return room.Id;
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicSetupPagingApi_Tests(AcademicSetupPagingFactory factory) : IClassFixture<AcademicSetupPagingFactory>
{
	public class Identifier { public Guid Id { get; set; } }
	[Fact]
	public async Task Foreign_room_reassignment_is_rejected_and_preserves_the_current_division_room()
	{
		using var client = factory.CreateClient();
		var branch = await factory.SeedBranchAsync();
		await factory.SetScopeAsync(null);
		var room = await factory.SeedRoomAsync("Owned classroom");
		var foreign = await factory.SeedRoomAsync("Foreign classroom", true);
		var created = await client.PostAsJsonAsync("/api/app/division", new { name = "Room test", strength = 20, standardId = branch.StandardId, roomNumber = room.ToString() });
		created.EnsureSuccessStatusCode();
		var division = (await created.Content.ReadFromJsonAsync<DivisionDto>())!;
		var response = await client.PutAsJsonAsync($"/api/app/division/{division.Id}", new {
			name = "Room test", strength = 20, standardId = branch.StandardId, roomNumber = foreign.ToString() });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.GetFromJsonAsync<DivisionDto>($"/api/app/division/{division.Id}"))!.RoomId.ShouldBe(room.ToString());
	}

	[Fact]
	public async Task Ambiguous_legacy_room_names_remain_unresolved_without_crashing_the_division_save()
	{
		using var client = factory.CreateClient();
		var branch = await factory.SeedBranchAsync();
		await factory.SetScopeAsync(null);
		await factory.SeedRoomAsync(" Legacy room ");
		await factory.SeedRoomAsync("Legacy room");
		var response = await client.PostAsJsonAsync("/api/app/division", new {
			name = "Legacy test", strength = 20, standardId = branch.StandardId, roomNumber = "Legacy room" });
		response.EnsureSuccessStatusCode();
		(await response.Content.ReadFromJsonAsync<DivisionDto>())!.RoomId.ShouldBe("Legacy room");
	}
	[Fact]
	public async Task Descendant_only_scope_is_applied_before_paging_at_every_hierarchy_level()
	{
		using var client = factory.CreateClient();
		var hidden = await factory.SeedBranchAsync();
		var allowed = await factory.SeedBranchAsync();
		await factory.SetScopeAsync(allowed);
		var targets = new Dictionary<string, Guid> {
			["education-board"] = allowed.BoardId, ["education-medium"] = allowed.MediumId,
			["education-medium-stage"] = allowed.StageId, ["division"] = allowed.DivisionId };
		foreach (var (route, expected) in targets)
		{
			var response = await client.GetAsync($"/api/app/{route}?MaxResultCount=1&Sorting=CreationTime");
			response.EnsureSuccessStatusCode();
			var page = (await response.Content.ReadFromJsonAsync<PagedResultDto<Identifier>>())!;
			page.TotalCount.ShouldBe(1, route);
			page.Items.Select(x => x.Id).ShouldBe([expected], route);
		}
		var hiddenTargets = new Dictionary<string, Guid> {
			["education-board"] = hidden.BoardId, ["education-medium"] = hidden.MediumId,
			["education-medium-stage"] = hidden.StageId, ["division"] = hidden.DivisionId };
		foreach (var (route, id) in hiddenTargets)
			(await client.GetAsync($"/api/app/{route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden, route);
	}

	[Fact]
	public async Task Tenant_wide_board_total_is_not_replaced_by_the_page_size()
	{
		using var client = factory.CreateClient();
		await factory.SeedBranchAsync();
		await factory.SeedBranchAsync();
		await factory.SetScopeAsync(null);
		var all = (await client.GetFromJsonAsync<PagedResultDto<EducationBoardDto>>("/api/app/education-board?MaxResultCount=1000"))!;
		var page = (await client.GetFromJsonAsync<PagedResultDto<EducationBoardDto>>("/api/app/education-board?MaxResultCount=1"))!;
		page.Items.Count.ShouldBe(1);
		page.TotalCount.ShouldBe(all.Items.Count);
		page.TotalCount.ShouldBeGreaterThan(1);
	}

	[Fact]
	public async Task Selected_year_hierarchy_is_complete_scoped_and_excludes_other_years()
	{
		using var client = factory.CreateClient();
		var other = await factory.SeedBranchAsync();
		var allowed = await factory.SeedBranchAsync();
		await factory.SetScopeAsync(allowed);
		factory.Commands.Clear();
		var response = await client.GetAsync($"/api/app/education-board/hierarchy?academicYearId={allowed.YearId}");
		response.EnsureSuccessStatusCode();
		var tree = (await response.Content.ReadFromJsonAsync<ListResultDto<EducationBoardDto>>())!;
		tree.Items.Select(x => x.Id).ShouldBe([allowed.BoardId]);
		tree.Items.Single().EducationMediums.Single().EducationMediumStages.Single().Standards.Single()
			.Divisions.Select(x => x.Id).ShouldBe([allowed.DivisionId]);
		factory.Commands.ShouldNotContain(sql => sql.Contains("\"BannerPicture\"", StringComparison.Ordinal));
		var hidden = (await client.GetFromJsonAsync<ListResultDto<EducationBoardDto>>(
			$"/api/app/education-board/hierarchy?academicYearId={other.YearId}"))!;
		hidden.Items.ShouldBeEmpty();
	}
}
