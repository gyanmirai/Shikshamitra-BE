using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("Standards", false)]
	[InlineData("StandardsByMediumStage", true)]
	public Task Records_field_options_distinct_selected_rows_keep_canonical_output_without_reloading_catalogues(string key, bool fullForm) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await PrepareFieldOptionsCatalogueAsync();
		var service = GetRequiredService<StudentRecordFieldOptionsService>();
		async Task<(StudentRecordRegistrationSourceDto Source, int Reads)> Read(int count)
		{
			var source = FieldOptionsRepeatedSource(fixture, key, count);
			return await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
				await service.PopulateAsync(source, true, fullForm);
				return (source, counter.ReadCommands.Count);
			});
		}
		var single = await Read(1); var multiple = await Read(4);
		var field = multiple.Source.Definition!.Sections.Single().Fields.Single();
		var parent = key == "Standards" ? (Guid?)null : fixture.StageId;
		for (var index = 0; index < 4; index++)
		{
			var canonical = await WithUnitOfWorkAsync(() => service.QueryAsync(multiple.Source, field, new()
			{ ParentId = parent, SelectedIds = [fixture.StandardIds[index]] }));
			JsonSerializer.Serialize(multiple.Source.OptionsByFieldPath[$"CLASS[{index}]"])
				.ShouldBe(JsonSerializer.Serialize(canonical));
		}
		if (key == "Standards")
		{
			var duplicate = multiple.Source.OptionsByField["CLASS"].Single(x => x.Value == fixture.StandardIds[0].ToString());
			duplicate.Label.ShouldBe($"Options duplicate class ({fixture.MediumName} / {fixture.StageName})");
			duplicate.IsActive.ShouldBeTrue();
			var other = multiple.Source.OptionsByField["CLASS"].Single(x => x.Value == fixture.StandardIds[2].ToString());
			other.Label.ShouldBe("Options duplicate class (Other options medium / Other options stage)");
		}
		else
		{
			var retained = multiple.Source.OptionsByField["CLASS"].Single(x => x.Value == fixture.StandardIds[2].ToString());
			retained.IsActive.ShouldBeFalse(); retained.Label.ShouldBe("Options duplicate class");
		}
		var ordered = multiple.Source.OptionsByFieldPath["CLASS[0]"];
		ordered.FindIndex(x => x.Value == fixture.StandardIds[1].ToString()).ShouldBeLessThan(ordered.FindIndex(x => x.Value == fixture.StandardIds[0].ToString()));
		ordered.Select(x => x.DisplayOrder).ShouldBe(Enumerable.Range(0, ordered.Count));
		var firstOption = ordered.Single(x => x.Value == fixture.StandardIds[0].ToString()); var label = firstOption.Label;
		firstOption.Label = "Only this detached row changed";
		multiple.Source.OptionsByFieldPath["CLASS[1]"].Single(x => x.Value == fixture.StandardIds[0].ToString()).Label.ShouldBe(label);
		firstOption.Label = label;
		Console.WriteLine($"Records options {key}, fullForm={fullForm}: one selected row={single.Reads}, four distinct selected rows={multiple.Reads} SQLite reads; canonical complete JSON parity passed.");
		multiple.Reads.ShouldBeLessThanOrEqualTo(single.Reads + 2, "Different retained selected IDs must not reload the same scoped academic catalogue.");
	});

	[Fact]
	public Task Records_field_options_catalogue_reuse_preserves_sources_parents_unavailable_ids_and_full_form_masks() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await PrepareFieldOptionsCatalogueAsync();
		var source = new StudentRecordRegistrationSourceDto { AcademicYearId = fixture.YearId, Definition = new() { Sections = [new() { Id = Guid.NewGuid() }] } };
		var section = source.Definition.Sections.Single(); var unavailable = Guid.NewGuid();
		void Add(string code, string key, Guid? current, Guid? original, string? parentField = null)
		{
			section.Fields.Add(new() { Id = Guid.NewGuid(), SectionId = section.Id, Code = code, FieldType = AdmissionFormFieldType.Select,
				OptionsJson = JsonSerializer.Serialize(new { source = key, parentField }) });
			source.CurrentValues[code] = JsonSerializer.SerializeToElement(current?.ToString() ?? "");
			source.OriginalValues[code] = JsonSerializer.SerializeToElement(original?.ToString() ?? "");
			source.EditableFieldCodes.Add(code); source.FormEditableFieldCodes.Add(code);
		}
		Add("BOARD", "EducationBoards", fixture.CanonicalBoardId, unavailable);
		Add("MEDIUM", "EducationMediums", fixture.MediumId, fixture.OtherMediumId, "BOARD_PARENT");
		Add("CLASS", "StandardsByMedium", fixture.StandardIds[0], fixture.StandardIds[2], "MEDIUM_PARENT");
		Add("STAGE_CLASS", "StandardsByMediumStage", fixture.StandardIds[0], fixture.StandardIds[2], "STAGE_PARENT");
		Add("DIVISION", "DivisionsByStandard", fixture.DivisionId, unavailable, "CLASS_PARENT");
		Add("NO_PARENT", "StandardsByMedium", fixture.StandardIds[0], unavailable, "MISSING_PARENT");
		Add("MASKED", "Standards", null, null); source.FormMaskedFieldCodes.Add("MASKED");
		foreach (var value in new[] { ("BOARD_PARENT", fixture.BoardId, fixture.BoardId), ("MEDIUM_PARENT", fixture.MediumId, fixture.OtherMediumId),
			("STAGE_PARENT", fixture.StageId, fixture.OtherStageId), ("CLASS_PARENT", fixture.StandardIds[0], fixture.StandardIds[0]) })
		{
			source.CurrentValues[value.Item1] = JsonSerializer.SerializeToElement(value.Item2.ToString());
			source.OriginalValues[value.Item1] = JsonSerializer.SerializeToElement(value.Item3.ToString());
		}
		source.FormEditableFieldCodes.Remove("NO_PARENT");
		var service = GetRequiredService<StudentRecordFieldOptionsService>();
		await WithUnitOfWorkAsync(() => service.PopulateAsync(source, true, fullForm: true));
		source.OptionsByField.ShouldNotContainKey("MASKED");
		foreach (var field in section.Fields.Where(x => x.Code != "MASKED"))
		{
			using var config = JsonDocument.Parse(field.OptionsJson!);
			var parentCode = config.RootElement.GetProperty("parentField").GetString();
			Guid? Parent(Dictionary<string, JsonElement> values) => parentCode != null && values.TryGetValue(parentCode, out var value) && Guid.TryParse(value.GetString(), out var id) ? id : null;
			var selected = new[] { source.CurrentValues[field.Code].GetString(), source.OriginalValues[field.Code].GetString() }
				.Where(x => Guid.TryParse(x, out _)).Select(x => Guid.Parse(x!)).Distinct().ToList();
			var available = source.FormEditableFieldCodes.Contains(field.Code);
			var expected = await WithUnitOfWorkAsync(() => service.QueryAsync(source, field, new() { ParentId = Parent(source.CurrentValues), SelectedIds = selected, SelectedOnly = !available }));
			if (Parent(source.CurrentValues) != Parent(source.OriginalValues))
				expected = expected.Concat(await WithUnitOfWorkAsync(() => service.QueryAsync(source, field, new() { ParentId = Parent(source.OriginalValues), SelectedIds = selected, SelectedOnly = true })))
					.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(o => o.IsActive).First()).ToList();
			JsonSerializer.Serialize(source.OptionsByField[field.Code]).ShouldBe(JsonSerializer.Serialize(expected));
		}
		var registryBoards = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionFormDataSourceRegistry>().GetOptionsAsync("EducationBoards", null, fixture.YearId));
		source.OptionsByField["BOARD"].Where(x => x.IsActive).Select(x => x.Value).ShouldBe(registryBoards.Select(x => x.Value));
		source.OptionsByField["BOARD"].Where(x => x.Label == fixture.BoardLabel && x.IsActive).ShouldHaveSingleItem().Value.ShouldBe(fixture.CanonicalBoardId.ToString());
		source.OptionsByField["BOARD"].Single(x => x.Value == unavailable.ToString()).Label.ShouldBe("Option unavailable");
		source.OptionsByField["NO_PARENT"].ShouldAllBe(x => !x.IsActive);
		source.OptionsByField["NO_PARENT"].Single(x => x.Value == fixture.StandardIds[0].ToString()).Label.ShouldBe("Options duplicate class");
		source.OptionsByField["DIVISION"].Single(x => x.Value == fixture.DivisionId.ToString()).Label.ShouldBe("Options division");
	});

	[Fact]
	public Task Records_field_options_catalogues_are_fresh_for_later_operations_and_reject_revoked_actor() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await PrepareFieldOptionsCatalogueAsync(); var service = GetRequiredService<StudentRecordFieldOptionsService>();
		var first = FieldOptionsRepeatedSource(fixture, "Standards", 1); await WithUnitOfWorkAsync(() => service.PopulateAsync(first, true));
		var added = await WithUnitOfWorkAsync(async () => await _standardRepository.InsertAsync(
			new Standard(Guid.NewGuid(), TestTenantId, "Later options class", 7, fixture.StageId, null), true));
		var second = FieldOptionsRepeatedSource(fixture, "Standards", 1); await WithUnitOfWorkAsync(() => service.PopulateAsync(second, true));
		first.OptionsByField["CLASS"].ShouldNotContain(x => x.Value == added.Id.ToString());
		second.OptionsByField["CLASS"].ShouldContain(x => x.Value == added.Id.ToString());
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try { await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.PopulateAsync(FieldOptionsRepeatedSource(fixture, "Standards", 1), true))); }
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
	});

	private sealed record FieldOptionsCatalogueFixture(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId,
		Guid OtherMediumId, Guid OtherStageId, Guid[] StandardIds, Guid DivisionId, string MediumName, string StageName,
		string BoardLabel, Guid OtherBoardId, Guid CanonicalBoardId);

	private async Task<FieldOptionsCatalogueFixture> PrepareFieldOptionsCatalogueAsync()
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		return await WithUnitOfWorkAsync(async () =>
		{
			var board = await _boardRepository.GetAsync(offering.BoardId); var originalMedium = await _mediumRepository.GetAsync(offering.MediumId);
			var originalStage = await _stageRepository.GetAsync(offering.StageId);
			var otherBoard = await _boardRepository.InsertAsync(new EducationBoard(Guid.NewGuid(), TestTenantId, board.Type, board.AcademicYearId), true);
			var medium = await _mediumRepository.InsertAsync(new EducationMedium(Guid.NewGuid(), TestTenantId, "Other options medium", otherBoard.Id), true);
			var stage = await _stageRepository.InsertAsync(new EducationMediumStage(Guid.NewGuid(), TestTenantId, "Other options stage", null, medium.Id), true);
			var ids = new List<Guid>();
			for (var index = 0; index < 4; index++)
				ids.Add((await _standardRepository.InsertAsync(new Standard(Guid.NewGuid(), TestTenantId,
					index % 2 == 0 ? "Options duplicate class" : $"Options class {index}", 7, index < 2 ? offering.StageId : stage.Id, null), true)).Id);
			var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, "Options division", ids[0], 20, ""), true);
			return new FieldOptionsCatalogueFixture(board.AcademicYearId, offering.BoardId, offering.MediumId, offering.StageId, medium.Id, stage.Id, ids.ToArray(), division.Id,
				originalMedium.Name, originalStage.Name, board.Type.ToString(), otherBoard.Id, new[] { board.Id, otherBoard.Id }.Order().First());
		});
	}

	private static StudentRecordRegistrationSourceDto FieldOptionsRepeatedSource(FieldOptionsCatalogueFixture fixture, string key, int count)
	{
		var field = new AdmissionFormFieldDto { Id = Guid.NewGuid(), Code = "CLASS", FieldType = AdmissionFormFieldType.Select,
			OptionsJson = JsonSerializer.Serialize(new { source = key, parentField = key == "Standards" ? null : "STAGE" }) };
		var section = new AdmissionFormSectionDto { Id = Guid.NewGuid(), IsRepeatable = true, MinEntries = 1, MaxEntries = 4, Fields = [field] };
		field.SectionId = section.Id;
		return new StudentRecordRegistrationSourceDto
		{
			AcademicYearId = fixture.YearId, Definition = new() { Sections = [section] }, EditableFieldCodes = ["CLASS"], FormEditableFieldCodes = ["CLASS"],
			CurrentValues = new() { ["CLASS"] = JsonSerializer.SerializeToElement(fixture.StandardIds.Take(count).Select(x => x.ToString()).ToArray()), ["STAGE"] = JsonSerializer.SerializeToElement(fixture.StageId.ToString()) },
			OriginalValues = new() { ["CLASS"] = JsonSerializer.SerializeToElement(fixture.StandardIds.Take(count).Select(x => x.ToString()).ToArray()), ["STAGE"] = JsonSerializer.SerializeToElement(fixture.StageId.ToString()) }
		};
	}
}
