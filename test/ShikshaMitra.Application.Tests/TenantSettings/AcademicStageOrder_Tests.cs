using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicStageOrder_Tests : ShikshaMitraApplicationTestBase<AcademicChildOrderTestModule>
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	[Fact]
	public async Task Saved_stage_order_is_used_by_hierarchy_medium_lists_repository_and_class_lookup_and_new_stages_append()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedAsync();
		var service = GetRequiredService<IEducationMediumStageAppService>();
		var expected = hierarchy.StageIds.Reverse().ToList();
		await service.ReorderAsync(hierarchy.MediumId, new() { Ids = expected });
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IEducationMediumStageRepository>();
			(await repository.GetEducationMediumStagesByEducationMediumIdAsync(hierarchy.MediumId)).Select(x => x.Id).ShouldBe(expected);
			var boards = await GetRequiredService<IEducationBoardAppService>().GetListAsync(new() { MaxResultCount = 1000 });
			boards.Items.Single(x => x.Id == hierarchy.BoardId).EducationMediums.Single(x => x.Id == hierarchy.MediumId)
				.EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
			(await GetRequiredService<IEducationMediumAppService>().GetAsync(hierarchy.MediumId))
				.EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
			var mediumList = await GetRequiredService<IEducationMediumAppService>().GetListAsync(new() { MaxResultCount = 1000 });
			mediumList.Items.Single(x => x.Id == hierarchy.MediumId).EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
			var stages = await service.GetListAsync(new() { MaxResultCount = 1000 });
			stages.Items.Where(x => x.EducationMediumId == hierarchy.MediumId).Select(x => x.Id).ShouldBe(expected);
			(await GetRequiredService<IStandardAppService>().GetStandardsByEducationMediumId(hierarchy.MediumId))
				.Select(x => x.Id).ShouldBe(hierarchy.StandardIds.Reverse());
			var sorted = await service.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000, Sorting = "Name ASC" });
			sorted.Items.Where(x => x.EducationMediumId == hierarchy.MediumId).Select(x => x.Name).ShouldBe(["Alpha", "Zulu"]);
		});
		var appended = await service.CreateAsync(new() { EducationMediumId = hierarchy.MediumId, Name = "Middle" });
		await WithUnitOfWorkAsync(async () =>
		{
			var persisted = await GetRequiredService<IEducationMediumStageRepository>().GetEducationMediumStagesByEducationMediumIdAsync(hierarchy.MediumId);
			persisted.Select(x => x.Id).ShouldBe(expected.Append(appended.Id));
			persisted.Select(x => x.SortOrder).Distinct().Count().ShouldBe(3);
		});
	}

	[Theory]
	[InlineData("duplicate")]
	[InlineData("partial")]
	[InlineData("unknown")]
	[InlineData("other-parent")]
	[InlineData("other-tenant")]
	[InlineData("empty")]
	public async Task Invalid_stage_lists_do_not_change_saved_order(string invalidKind)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedAsync();
		var service = GetRequiredService<IEducationMediumStageAppService>();
		var expected = hierarchy.StageIds.Reverse().ToList();
		await service.ReorderAsync(hierarchy.MediumId, new() { Ids = expected });
		var foreignId = Guid.NewGuid();
		if (invalidKind is "other-parent" or "other-tenant")
		{
			using var foreignTenant = GetRequiredService<ICurrentTenant>().Change(invalidKind == "other-tenant" ? Guid.NewGuid() : TenantId);
			foreignId = (await SeedAsync()).StageIds[0];
		}
		List<Guid> invalidIds = invalidKind switch
		{
			"duplicate" => [hierarchy.StageIds[0], hierarchy.StageIds[0]],
			"partial" => [hierarchy.StageIds[0]],
			"empty" => [],
			_ => [hierarchy.StageIds[0], foreignId]
		};
		Func<Task> reorder = () => service.ReorderAsync(hierarchy.MediumId, new() { Ids = invalidIds });
		if (invalidKind == "empty") await Should.ThrowAsync<AbpValidationException>(reorder);
		else await Should.ThrowAsync<BusinessException>(reorder);
		await WithUnitOfWorkAsync(async () =>
		{
			var rows = await GetRequiredService<IEducationMediumStageRepository>().GetEducationMediumStagesByEducationMediumIdAsync(hierarchy.MediumId);
			rows.Select(x => x.Id).ShouldBe(expected);
		});
	}

	[Theory]
	[InlineData("standard", true)]
	[InlineData("division", true)]
	[InlineData("standard", false)]
	[InlineData("division", false)]
	public async Task Child_only_scope_requires_visibility_of_every_stage(string childKind, bool complete)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedAsync();
		var scope = new UserScopeContext();
		if (childKind == "standard") scope.StandardIds.AddRange(hierarchy.StandardIds.Take(complete ? 2 : 1));
		else scope.DivisionIds.AddRange(hierarchy.DivisionIds.Take(complete ? 2 : 1));
		GetRequiredService<IUserScopeService>().GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>()).Returns(scope);
		var expected = complete ? hierarchy.StageIds.Reverse().ToList() : hierarchy.StageIds.ToList();
		Func<Task> reorder = () => GetRequiredService<IEducationMediumStageAppService>().ReorderAsync(hierarchy.MediumId,
			new() { Ids = hierarchy.StageIds.Reverse().ToList() });
		if (complete) await reorder();
		else await Should.ThrowAsync<AbpAuthorizationException>(reorder);
		await WithUnitOfWorkAsync(async () =>
		{
			var rows = await GetRequiredService<IEducationMediumStageRepository>().GetListAsync(x => x.EducationMediumId == hierarchy.MediumId);
			rows.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Id).ShouldBe(expected);
		});
	}

	private IDisposable SignIn() => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
		new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));

	private async Task<Hierarchy> SeedAsync()
	{
		Hierarchy result = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = GetRequiredService<ICurrentTenant>().Id!.Value;
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, $"Stage {Guid.NewGuid():N}", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, "English", board.Id), true);
			var stageIds = new List<Guid>(); var standardIds = new List<Guid>(); var divisionIds = new List<Guid>();
			foreach (var name in new[] { "Alpha", "Zulu" })
			{
				var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, name, null, medium.Id), true);
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, $"Class {stageIds.Count + 1}", 6, stage.Id, null), true);
				var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, "A", standard.Id, 30, ""), true);
				stageIds.Add(stage.Id); standardIds.Add(standard.Id); divisionIds.Add(division.Id);
			}
			result = new(board.Id, medium.Id, stageIds.ToArray(), standardIds.ToArray(), divisionIds.ToArray());
		});
		return result;
	}

	private sealed record Hierarchy(Guid BoardId, Guid MediumId, Guid[] StageIds, Guid[] StandardIds, Guid[] DivisionIds);
}
