using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class StandardLookupApi_Tests
{
	[Theory]
	[InlineData(1, "wide", "none")]
	[InlineData(4, "wide", "none")]
	[InlineData(4, "standard", "none")]
	[InlineData(4, "division", "none")]
	[InlineData(4, "wide", "stage")]
	[InlineData(4, "wide", "board")]
	public async Task Medium_standard_selector_batches_stage_reads_without_changing_full_dto_scope_order_or_occupancy(int stageCount, string scopeKind, string deleted)
	{
		using var client = factory.CreateClient();
		factory.Scope = new() { HasWildcardAcademicYear = true };
		var fixture = await SeedMediumReadAsync(stageCount, deleted);
		factory.Scope = scopeKind switch {
			"standard" => new() { StandardIds = [fixture.First.StandardId] },
			"division" => new() { DivisionIds = [fixture.First.DivisionId] },
			_ => new() { HasWildcardAcademicYear = true }
		};
		var expected = await ReadLegacyMediumOracleAsync(fixture.First.MediumId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "No.New.Action.Grant");
		factory.Commands.Clear();
		var response = await client.GetAsync($"/api/education-medium/{fixture.First.MediumId}/standards");
		response.EnsureSuccessStatusCode();
		var actual = (await response.Content.ReadFromJsonAsync<List<StandardDto>>())!;
		var json = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
		JsonSerializer.Serialize(actual, json).ShouldBe(JsonSerializer.Serialize(expected, json), "All persisted standard/division/shift/audit fields must match the legacy complete selector.");
		if (deleted == "board") actual.ShouldBeEmpty();
		else if (scopeKind == "wide") actual.Select(row => row.Id).ShouldBe(fixture.ExpectedOrder);
		else actual.ShouldHaveSingleItem().Id.ShouldBe(fixture.First.StandardId);
		if (deleted != "board")
		{
			var first = actual.Single(row => row.Id == fixture.First.StandardId);
			first.Shift.ShiftName.ShouldBe("Selector shift");
			first.AgeLowerLimit.ShouldBe(6);
			if (scopeKind == "standard") first.Divisions.ShouldBeEmpty();
			else
			{
				var occupied = first.Divisions.Single(row => row.Id == fixture.First.DivisionId);
				(occupied.CurrentStudentCount, occupied.HeadCount, occupied.MaleCount, occupied.FemaleCount, occupied.AvailableSeats).ShouldBe((2, 2, 1, 1, 38));
				if (scopeKind == "division") first.Divisions.ShouldHaveSingleItem();
				else first.Divisions.Select(row => row.Name).ShouldBe(["B", "A"]);
			}
		}
		var standardReads = factory.Commands.Count(sql => Regex.Match(sql, "\\bFROM\\s+\"(?<table>[^\"]+)\"", RegexOptions.IgnoreCase)
			.Groups["table"].Value.EndsWith("Standards", StringComparison.Ordinal));
		standardReads.ShouldBe(deleted == "board" ? 0 : 1, $"The actual HTTP selector must use one standard-root read for {stageCount} stages; observed {standardReads}. Full response/scope/occupancy parity already passed.");
	}

	private async Task<MediumReadFixture> SeedMediumReadAsync(int count, string deleted)
	{
		var first = await factory.SeedBranchAsync();
		using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var work = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var stages = services.GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		var standards = services.GetRequiredService<IStandardRepository>();
		var divisions = services.GetRequiredService<IRepository<Division, Guid>>();
		var shift = new ShiftManagement(Guid.NewGuid(), tenantId, "Selector shift", new TimeOnly(8, 0), new TimeOnly(14, 0));
		await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(shift, true);
		var stageRows = new List<EducationMediumStage> { await stages.GetAsync(first.StageId) };
		for (var index = 1; index < count; index++)
		{
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(first.MediumId, $"Selector stage {index}");
			await stages.InsertAsync(stage, true); stageRows.Add(stage);
		}
		var roots = new Dictionary<Guid, Guid[]>();
		foreach (var stage in stageRows)
		{
			var one = stage.Id == first.StageId ? await standards.GetAsync(first.StandardId) :
				await standards.InsertAsync(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 1", 6, shift.Id), true);
			await services.GetRequiredService<StandardManager>().UpdateAsync(one, one.Name, one.AgeLowerLimit, shift.Id);
			await standards.UpdateAsync(one, true);
			var two = await standards.InsertAsync(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 2", 7, null), true);
			services.GetRequiredService<StandardManager>().Reorder([one, two], [two.Id, one.Id]);
			await standards.UpdateManyAsync([one, two], true); roots[stage.Id] = [two.Id, one.Id];
			var a = stage.Id == first.StageId ? await divisions.GetAsync(first.DivisionId) :
				await divisions.InsertAsync(await services.GetRequiredService<DivisionManager>().CreateAsync(one.Id, "A", 40), true);
			var b = await divisions.InsertAsync(await services.GetRequiredService<DivisionManager>().CreateAsync(one.Id, "B", 41, "Selector room"), true);
			services.GetRequiredService<DivisionManager>().Reorder([a, b], [b.Id, a.Id]);
			await divisions.UpdateManyAsync([a, b], true);
		}
		services.GetRequiredService<EducationMediumStageManager>().Reorder(stageRows, stageRows.Select(row => row.Id).Reverse().ToArray());
		await stages.UpdateManyAsync(stageRows, true);
		foreach (var (status, gender, remove) in new[] {
			(StudentLifeCycleStatusEnum.Enrolled, GenderEnum.Male, false),
			(StudentLifeCycleStatusEnum.Enrolled, GenderEnum.Female, false),
			(StudentLifeCycleStatusEnum.InActive, GenderEnum.Male, false),
			(StudentLifeCycleStatusEnum.Enrolled, GenderEnum.Male, true) })
		{
			var student = new Student(Guid.NewGuid(), tenantId, "Selector", null, "Pupil", first.YearId, first.BoardId, first.MediumId,
				first.StageId, first.StandardId, first.DivisionId, status: status);
			await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			await services.GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(
				new StudentAdditionalDetail(student.Id, tenantId, null, gender, null, "", null, null), true);
			if (remove) await services.GetRequiredService<IStudentRepository>().DeleteAsync(student, true);
		}
		var deletedStageId = deleted == "stage" ? stageRows[^1].Id : Guid.Empty;
		if (deleted == "stage") await stages.DeleteAsync(deletedStageId, true);
		if (deleted == "board") await services.GetRequiredService<IRepository<EducationBoard, Guid>>().DeleteAsync(first.BoardId, true);
		await work.CompleteAsync();
		return new(first, stageRows.AsEnumerable().Reverse().Where(row => row.Id != deletedStageId).SelectMany(row => roots[row.Id]).ToArray());
	}

	// This oracle deliberately keeps the previous per-stage repository traversal.
	// Production may batch those reads, but its full public DTO contract must remain identical.
	private async Task<List<StandardDto>> ReadLegacyMediumOracleAsync(Guid mediumId)
	{
		using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var work = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var stages = await services.GetRequiredService<IEducationMediumStageRepository>().GetEducationMediumStagesByEducationMediumIdAsync(mediumId);
		var rows = new List<Standard>();
		foreach (var stage in stages.OrderBy(row => row.SortOrder).ThenBy(row => row.Name).ThenBy(row => row.Id))
			rows.AddRange(await services.GetRequiredService<IStandardRepository>().GetStandardsByEducationMediumStageIdAsync(stage.Id));
		rows = await services.GetRequiredService<AcademicOrganizationScopeAccessService>().FilterStandardsAsync(rows);
		var result = services.GetRequiredService<IObjectMapper>().Map<List<Standard>, List<StandardDto>>(rows);
		var ids = result.SelectMany(row => row.Divisions).Select(row => row.Id).ToArray();
		var occupancy = await services.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(
			(await services.GetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync())
				.Where(row => row.DivisionId.HasValue && ids.Contains(row.DivisionId.Value) && row.Status == StudentLifeCycleStatusEnum.Enrolled)
				.Select(row => new { row.DivisionId, Gender = row.AdditionalDetail != null ? row.AdditionalDetail.Gender : null }));
		foreach (var division in result.SelectMany(row => row.Divisions))
		{
			var members = occupancy.Where(row => row.DivisionId == division.Id).ToList();
			division.CurrentStudentCount = division.HeadCount = members.Count;
			division.MaleCount = members.Count(row => row.Gender == GenderEnum.Male);
			division.FemaleCount = members.Count(row => row.Gender == GenderEnum.Female);
			division.AvailableSeats = Math.Max(division.Strength - members.Count, 0);
		}
		// Filtering prunes tracked navigation collections; this reference read must
		// not persist that material or change the audit values before the HTTP read.
		return result;
	}

	private sealed record MediumReadFixture(AcademicSetupPagingFactory.Branch First, Guid[] ExpectedOrder);
}
