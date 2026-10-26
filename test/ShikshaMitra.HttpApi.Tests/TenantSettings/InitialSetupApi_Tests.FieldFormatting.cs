using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.InitialSetup;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InitialSetupApi_Tests
{
	[Theory]
	[InlineData(AcademicTextCase.CamelCase, AcademicTextCase.CamelCase, "divisionOne")]
	[InlineData(AcademicTextCase.Default, AcademicTextCase.CamelCase, "division one")]
	[InlineData(AcademicTextCase.CamelCase, AcademicTextCase.Capitalize, "divisionOne")]
	[InlineData(AcademicTextCase.CamelCase, AcademicTextCase.PascalCase, "divisionOne")]
	[InlineData(AcademicTextCase.Uppercase, AcademicTextCase.CamelCase, "DIVISION ONE")]
	[InlineData(AcademicTextCase.CamelCase, AcademicTextCase.Uppercase, "divisionOne")]
	public async Task Repeated_raw_structure_drafts_reuse_formatted_names_and_preserve_them_during_other_edits(
		AcademicTextCase initial, AcademicTextCase changed, string savedName)
	{
		using var school = await NewSchoolAsync();
		(await school.PutAsJsonAsync("/api/app/academic-field-formatting", new AcademicFieldFormattingDto
		{
			Medium = initial, Stage = initial, Standard = initial, Division = initial
		})).EnsureSuccessStatusCode();
		var progress = await PrepareYearStepAsync(school);
		progress = await PutAsync(school, "academic-year", new SaveSetupYearInput
		{
			ConcurrencyStamp = progress.ConcurrencyStamp, Name = "Formatting year",
			StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 5, 31)
		});
		var draft = progress.Draft;
		draft.BoardType = AcademicSetups.EducationBoards.EducationBoardType.CBSE;
		draft.Medium = "english medium"; draft.Stage = "primary stage"; draft.ClassName = "standard one";
		draft.DivisionName = "division one"; draft.AgeLowerLimit = 6; draft.Strength = 30;
		progress = await PutAsync(school, "structure", new SaveSetupStructureInput { ConcurrencyStamp = progress.ConcurrencyStamp, Draft = draft });
		var divisionId = progress.DivisionId;
		(await school.PutAsJsonAsync("/api/app/academic-field-formatting", new AcademicFieldFormattingDto
		{
			Medium = changed, Stage = changed, Standard = changed, Division = changed
		})).EnsureSuccessStatusCode();
		progress = await PutAsync(school, "structure", new SaveSetupStructureInput { ConcurrencyStamp = progress.ConcurrencyStamp, Draft = draft });
		progress.DivisionId.ShouldBe(divisionId);
		draft.AgeLowerLimit = 7; draft.Strength = 35;
		progress = await PutAsync(school, "structure", new SaveSetupStructureInput { ConcurrencyStamp = progress.ConcurrencyStamp, Draft = draft });
		progress.DivisionId.ShouldBe(divisionId);
		var division = (await school.GetFromJsonAsync<DivisionDto>($"/api/app/division/{divisionId}"))!;
		division.Name.ShouldBe(savedName);
		division.Strength.ShouldBe(35);
		var standard = (await school.GetFromJsonAsync<StandardDto>($"/api/app/standard/{division.StandardId}"))!;
		standard.Name.ShouldBe(initial switch
		{
			AcademicTextCase.Default => "standard one",
			AcademicTextCase.Uppercase => "STANDARD ONE",
			_ => "standardOne"
		});
		standard.AgeLowerLimit.ShouldBe(7);
	}
}
