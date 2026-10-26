using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class AcademicChildOrderTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var scope = Substitute.For<IUserScopeService>();
		scope.GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>())
			.Returns(new UserScopeContext { IsAdmin = true });
		context.Services.AddSingleton(scope);
	}
}

public class AcademicChildOrder_Tests : ShikshaMitraApplicationTestBase<AcademicChildOrderTestModule>
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	[Fact]
	public async Task Reordered_classes_persist_in_the_requested_order()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		Guid stageId = default;
		Guid[] ids = [];
		await WithUnitOfWorkAsync(async () =>
		{
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, "Order test year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, "English", board.Id), true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, "Primary", null, medium.Id), true);
			stageId = stage.Id;
			var repository = GetRequiredService<IRepository<Standard, Guid>>();
			var first = await repository.InsertAsync(new(Guid.NewGuid(), TenantId, "Class 1", 6, stage.Id, null), true);
			var second = await repository.InsertAsync(new(Guid.NewGuid(), TenantId, "Class 2", 7, stage.Id, null), true);
			ids = [second.Id, first.Id];
		});
		await GetRequiredService<IStandardAppService>().ReorderAsync(stageId, new ReorderAcademicChildrenDto { Ids = ids.ToList() });
		await WithUnitOfWorkAsync(async () =>
		{
			var standards = await GetRequiredService<IRepository<Standard, Guid>>().GetListAsync(x => x.EducationMediumStageId == stageId);
			standards.OrderBy(x => x.SortOrder).Select(x => x.Id).ShouldBe(ids);
		});
	}

	[Fact]
	public async Task Hierarchy_lookup_and_new_records_use_the_saved_class_and_division_order()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedHierarchyAsync();
		var standards = GetRequiredService<IStandardAppService>();
		var divisions = GetRequiredService<IDivisionAppService>();
		var classOrder = hierarchy.StandardIds.Reverse().ToList();
		var divisionOrder = hierarchy.DivisionIds.Reverse().ToList();
		await standards.ReorderAsync(hierarchy.StageId, new() { Ids = classOrder });
		await divisions.ReorderAsync(hierarchy.StandardIds[0], new() { Ids = divisionOrder });

		await WithUnitOfWorkAsync(async () =>
		{
			var lookup = await standards.GetStandardsByEducationMediumId(hierarchy.MediumId);
			lookup.Select(x => x.Id).ShouldBe(classOrder);
			lookup.Single(x => x.Id == hierarchy.StandardIds[0]).Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
			var tree = await GetRequiredService<IEducationBoardAppService>().GetListAsync(new() { MaxResultCount = 1000 });
			var stage = tree.Items.Single(x => x.Id == hierarchy.BoardId).EducationMediums
				.Single(x => x.Id == hierarchy.MediumId).EducationMediumStages.Single(x => x.Id == hierarchy.StageId);
			stage.Standards.Select(x => x.Id).ShouldBe(classOrder);
			stage.Standards.Single(x => x.Id == hierarchy.StandardIds[0]).Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
			(await standards.GetAsync(hierarchy.StandardIds[0])).Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
		});

		var newStandard = await standards.CreateAsync(new() { EducationMediumStageId = hierarchy.StageId, Name = "Class 3", AgeLowerLimit = 8 });
		var newDivision = await divisions.CreateAsync(new() { StandardId = hierarchy.StandardIds[0], Name = "C", Strength = 30 });
		await WithUnitOfWorkAsync(async () =>
		{
			var lookup = await standards.GetStandardsByEducationMediumId(hierarchy.MediumId);
			lookup.Select(x => x.Id).ShouldBe(classOrder.Append(newStandard.Id));
			lookup.Single(x => x.Id == hierarchy.StandardIds[0]).Divisions.Select(x => x.Id).ShouldBe(divisionOrder.Append(newDivision.Id));
			var classList = await standards.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
			classList.Items.Where(x => x.EducationMediumStageId == hierarchy.StageId).Select(x => x.Id).ShouldBe(classOrder.Append(newStandard.Id));
			var divisionList = await divisions.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
			divisionList.Items.Where(x => x.StandardId == hierarchy.StandardIds[0]).Select(x => x.Id).ShouldBe(divisionOrder.Append(newDivision.Id));
			var sortedClasses = await standards.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000, Sorting = "Name DESC" });
			sortedClasses.Items.Where(x => x.EducationMediumStageId == hierarchy.StageId).Select(x => x.Name).ShouldBe(["Class 3", "Class 2", "Class 1"]);
			var sortedDivisions = await divisions.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000, Sorting = "Name DESC" });
			sortedDivisions.Items.Where(x => x.StandardId == hierarchy.StandardIds[0]).Select(x => x.Name).ShouldBe(["C", "B", "A"]);
		});
	}

	[Theory]
	[InlineData("duplicate")]
	[InlineData("partial")]
	[InlineData("empty")]
	[InlineData("unknown")]
	[InlineData("other-parent")]
	[InlineData("other-tenant")]
	public async Task Invalid_sibling_lists_do_not_change_class_or_division_order(string invalidKind)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedHierarchyAsync();
		var standardApp = GetRequiredService<IStandardAppService>();
		var divisionApp = GetRequiredService<IDivisionAppService>();
		await standardApp.ReorderAsync(hierarchy.StageId, new() { Ids = hierarchy.StandardIds.Reverse().ToList() });
		await divisionApp.ReorderAsync(hierarchy.StandardIds[0], new() { Ids = hierarchy.DivisionIds.Reverse().ToList() });
		var invalidClass = Guid.NewGuid();
		var invalidDivision = Guid.NewGuid();
		if (invalidKind is "other-parent" or "other-tenant")
		{
			using var dataTenant = GetRequiredService<ICurrentTenant>().Change(invalidKind == "other-tenant" ? Guid.NewGuid() : TenantId);
			var other = await SeedHierarchyAsync();
			invalidClass = other.StandardIds[0];
			invalidDivision = other.DivisionIds[0];
		}
		Func<Task> reorderClasses = () => standardApp.ReorderAsync(hierarchy.StageId,
			new() { Ids = InvalidIds(hierarchy.StandardIds, invalidKind, invalidClass) });
		Func<Task> reorderDivisions = () => divisionApp.ReorderAsync(hierarchy.StandardIds[0],
			new() { Ids = InvalidIds(hierarchy.DivisionIds, invalidKind, invalidDivision) });
		if (invalidKind == "empty")
		{
			await Should.ThrowAsync<AbpValidationException>(reorderClasses);
			await Should.ThrowAsync<AbpValidationException>(reorderDivisions);
		}
		else
		{
			await Should.ThrowAsync<BusinessException>(reorderClasses);
			await Should.ThrowAsync<BusinessException>(reorderDivisions);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var classes = await GetRequiredService<IRepository<Standard, Guid>>().GetListAsync(x => x.EducationMediumStageId == hierarchy.StageId);
			classes.OrderBy(x => x.SortOrder).Select(x => x.Id).ShouldBe(hierarchy.StandardIds.Reverse());
			var childDivisions = await GetRequiredService<IRepository<Division, Guid>>().GetListAsync(x => x.StandardId == hierarchy.StandardIds[0]);
			childDivisions.OrderBy(x => x.SortOrder).Select(x => x.Id).ShouldBe(hierarchy.DivisionIds.Reverse());
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task User_without_access_to_all_siblings_cannot_reorder_classes_or_divisions(bool hasPartialScope)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedHierarchyAsync();
		var scope = new UserScopeContext();
		if (hasPartialScope)
		{
			scope.StandardIds.Add(hierarchy.StandardIds[0]);
			scope.DivisionIds.Add(hierarchy.DivisionIds[0]);
		}
		GetRequiredService<IUserScopeService>().GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>())
			.Returns(scope);
		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IStandardAppService>()
			.ReorderAsync(hierarchy.StageId, new() { Ids = hierarchy.StandardIds.ToList() }));
		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IDivisionAppService>()
			.ReorderAsync(hierarchy.StandardIds[0], new() { Ids = hierarchy.DivisionIds.ToList() }));
	}

	private IDisposable SignIn() => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
		new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
		new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));

	[Fact]
	public async Task Complete_child_only_scope_allows_reorder_without_ancestor_assignments()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		var hierarchy = await SeedHierarchyAsync();
		GetRequiredService<IUserScopeService>().GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>())
			.Returns(new UserScopeContext
			{
				StandardIds = hierarchy.StandardIds.ToList(),
				DivisionIds = hierarchy.DivisionIds.ToList()
			});
		await GetRequiredService<IStandardAppService>().ReorderAsync(hierarchy.StageId,
			new() { Ids = hierarchy.StandardIds.Reverse().ToList() });
		await GetRequiredService<IDivisionAppService>().ReorderAsync(hierarchy.StandardIds[0],
			new() { Ids = hierarchy.DivisionIds.Reverse().ToList() });
		await WithUnitOfWorkAsync(async () =>
		{
			var standards = await GetRequiredService<IRepository<Standard, Guid>>().GetListAsync(x => x.EducationMediumStageId == hierarchy.StageId);
			standards.OrderBy(x => x.SortOrder).Select(x => x.Id).ShouldBe(hierarchy.StandardIds.Reverse());
			var divisions = await GetRequiredService<IRepository<Division, Guid>>().GetListAsync(x => x.StandardId == hierarchy.StandardIds[0]);
			divisions.OrderBy(x => x.SortOrder).Select(x => x.Id).ShouldBe(hierarchy.DivisionIds.Reverse());
		});
	}

	private static List<Guid> InvalidIds(Guid[] ids, string kind, Guid foreignId) => kind switch
	{
		"duplicate" => [ids[0], ids[0]],
		"partial" => [ids[0]],
		"empty" => [],
		_ => [ids[0], foreignId]
	};

	private async Task<Hierarchy> SeedHierarchyAsync()
	{
		Hierarchy result = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = GetRequiredService<ICurrentTenant>().Id!.Value;
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, $"Order {Guid.NewGuid():N}", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, "English", board.Id), true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId, "Primary", null, medium.Id), true);
			var standards = GetRequiredService<IRepository<Standard, Guid>>();
			var first = await standards.InsertAsync(new(Guid.NewGuid(), tenantId, "Class 1", 6, stage.Id, null), true);
			var second = await standards.InsertAsync(new(Guid.NewGuid(), tenantId, "Class 2", 7, stage.Id, null), true);
			var divisions = GetRequiredService<IRepository<Division, Guid>>();
			var a = await divisions.InsertAsync(new(Guid.NewGuid(), tenantId, "A", first.Id, 30, ""), true);
			var b = await divisions.InsertAsync(new(Guid.NewGuid(), tenantId, "B", first.Id, 30, ""), true);
			result = new(board.Id, medium.Id, stage.Id, [first.Id, second.Id], [a.Id, b.Id]);
		});
		return result;
	}

	private sealed record Hierarchy(Guid BoardId, Guid MediumId, Guid StageId, Guid[] StandardIds, Guid[] DivisionIds);
}
