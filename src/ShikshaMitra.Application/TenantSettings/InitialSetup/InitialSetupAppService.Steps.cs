using System.Text.Json;
using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings.InitialSetup;

public partial class InitialSetupAppService
{
	public async Task<InitialSetupDto> SaveProfileAsync(SaveSetupProfileInput input)
	{
		var progress = await EditableAsync(input);
		var draft = InitialSetupStatusReader.Deserialize(progress.DraftJson);
		progress.SaveDraft(1, JsonSerializer.Serialize(draft));
		draft.Profile = await profileWriter.WriteAsync(input);
		progress.SaveDraft(1, JsonSerializer.Serialize(draft));
		InitialSetupDraftState.Confirm(progress, draft, 1);
		progress.Advance(1);
		return await SaveAsync(progress);
	}
	public async Task<InitialSetupDto> SaveYearAsync(SaveSetupYearInput input)
	{
		var progress = await EditableAsync(input);
		var draft = InitialSetupStatusReader.Deserialize(progress.DraftJson);
		progress.SaveDraft(2, JsonSerializer.Serialize(draft));
		var year = await academicWriter.WriteYearAsync(input, progress.AcademicYearId);
		progress.SelectYear(year.Id);
		draft.YearName = year.Name; draft.StartDate = year.StartDate; draft.EndDate = year.EndDate;
		draft.ExistingYearId = year.Id;
		progress.SaveDraft(2, JsonSerializer.Serialize(draft));
		InitialSetupDraftState.Confirm(progress, draft, 2);
		progress.Advance(2);
		return await SaveAsync(progress);
	}
	public async Task<InitialSetupDto> SaveStructureAsync(SaveSetupStructureInput input)
	{
		var progress = await EditableAsync(input);
		Validate(input.Draft);
		var draft = InitialSetupStatusReader.Deserialize(progress.DraftJson);
		progress.SaveDraft(3, JsonSerializer.Serialize(draft));
		var divisionId = await academicWriter.WriteStructureAsync(progress.AcademicYearId, input.Draft);
		draft.BoardType = input.Draft.BoardType; draft.Medium = input.Draft.Medium.Trim();
		draft.Stage = input.Draft.Stage.Trim(); draft.ClassName = input.Draft.ClassName.Trim();
		draft.AgeLowerLimit = input.Draft.AgeLowerLimit; draft.DivisionName = input.Draft.DivisionName.Trim();
		draft.Strength = input.Draft.Strength; draft.RoomNumber = input.Draft.RoomNumber?.Trim() ?? "";
		progress.SaveDraft(3, JsonSerializer.Serialize(draft));
		InitialSetupDraftState.Confirm(progress, draft, 3);
		progress.SelectDivision(divisionId);
		progress.Advance(3);
		return await SaveAsync(progress);
	}
}
