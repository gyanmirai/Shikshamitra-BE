using System.Linq;
using System.Text.Json;
using Volo.Abp.Data;

namespace ShikshaMitra.TenantSettings.InitialSetup;

/// <summary>Drafts remain editable without treating uncommitted configuration as completed work.</summary>
public static class InitialSetupDraftState
{
	public static void Confirm(InstituteSetupProgress progress, InitialSetupDraftDto draft, int step) =>
		progress.SetProperty("ConfirmedStep" + step, Projection(draft, step));
	public static int[] PendingSteps(InstituteSetupProgress progress)
	{
		if (progress.CompletedAt.HasValue) return [];
		var draft = InitialSetupStatusReader.Deserialize(progress.DraftJson);
		return new[] { 1, 2, 3 }.Where(step => progress.FurthestStep > step &&
			NormalizeConfirmed(progress.GetProperty<string>("ConfirmedStep" + step), step) != Projection(draft, step)).ToArray();
	}
	private static string? NormalizeConfirmed(string? confirmed, int step)
	{
		if (step != 3 || string.IsNullOrWhiteSpace(confirmed)) return confirmed;
		try
		{
			var draft = JsonSerializer.Deserialize<InitialSetupDraftDto>(confirmed);
			return draft == null ? confirmed : Projection(draft, step);
		}
		catch (JsonException) { return confirmed; }
	}
	private static string Projection(InitialSetupDraftDto draft, int step) => step switch
	{
		1 => JsonSerializer.Serialize(draft.Profile),
		2 => JsonSerializer.Serialize(new { draft.YearName, draft.StartDate, draft.EndDate, draft.ExistingYearId }),
		_ => JsonSerializer.Serialize(new { draft.BoardType, draft.CustomBoardName, draft.Medium, draft.Stage, draft.ClassName,
			draft.AgeLowerLimit, draft.DivisionName, draft.Strength })
	};
}
