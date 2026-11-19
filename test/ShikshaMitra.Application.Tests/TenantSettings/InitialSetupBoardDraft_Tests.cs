using System;
using System.Text.Json;
using ShikshaMitra.TenantSettings.InitialSetup;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InitialSetupBoardDraft_Tests
{
	[Fact]
	public void Legacy_confirmed_structure_ignores_removed_room_field_without_reopening_the_step()
	{
		var progress = new InstituteSetupProgress(Guid.NewGuid());
		for (var step = 0; step <= 3; step++) progress.Advance(step);
		var legacy = "{\"BoardType\":0,\"Medium\":\"English\",\"Stage\":\"Primary\",\"ClassName\":\"Class 1\",\"AgeLowerLimit\":6,\"DivisionName\":\"A\",\"Strength\":50,\"RoomNumber\":\"101\"}";
		progress.SaveDraft(3, legacy);
		var draft = InitialSetupStatusReader.Deserialize(legacy);
		InitialSetupDraftState.Confirm(progress, draft, 1);
		InitialSetupDraftState.Confirm(progress, draft, 2);
		progress.SetProperty("ConfirmedStep3", legacy);
		InitialSetupDraftState.PendingSteps(progress).ShouldBeEmpty();
		JsonSerializer.Serialize(draft).ShouldNotContain("RoomNumber");
	}
	[Fact]
	public void Changing_board_type_marks_the_structure_as_pending()
	{
		var progress = new InstituteSetupProgress(Guid.NewGuid());
		for (var step = 0; step <= 3; step++) progress.Advance(step);
		var draft = new InitialSetupDraftDto { BoardType = EducationBoardType.CBSE };
		for (var step = 1; step <= 3; step++) InitialSetupDraftState.Confirm(progress, draft, step);
		draft.BoardType = EducationBoardType.GSEB;
		progress.SaveDraft(3, JsonSerializer.Serialize(draft));
		InitialSetupDraftState.PendingSteps(progress).ShouldBe(new[] { 3 });
	}
}
