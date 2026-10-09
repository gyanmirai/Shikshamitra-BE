using System;
using ShikshaMitra.TenantSettings.InitialSetup;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InitialSetupProgress_Tests
{
	[Fact]
	public void Future_steps_and_unreviewed_completion_are_rejected()
	{
		var progress = new InstituteSetupProgress(Guid.NewGuid());
		Should.Throw<BusinessException>(() => progress.SaveDraft(2, "{}"))
			.Code.ShouldBe(InitialSetupErrorCodes.EarlierStepsRequired);
		Should.Throw<BusinessException>(() => progress.Advance(4));
		Should.Throw<BusinessException>(() => progress.Complete(DateTime.UtcNow, Guid.NewGuid()));
		progress.CurrentStep.ShouldBe(0);
	}
	[Fact]
	public void Reviewing_an_earlier_step_preserves_later_progress_and_records()
	{
		var progress = Prepared(); var year = progress.AcademicYearId; var division = progress.DivisionId;
		progress.SaveDraft(1, "{\"profile\":{\"city\":\"Surat\"}}");
		progress.CurrentStep.ShouldBe(1); progress.FurthestStep.ShouldBe(5);
		progress.AcademicYearId.ShouldBe(year); progress.DivisionId.ShouldBe(division);
		progress.SaveDraft(5, progress.DraftJson); progress.CurrentStep.ShouldBe(5);
	}
	[Fact]
	public void Selecting_another_year_requires_its_own_academic_structure()
	{
		var progress = Prepared();
		progress.SelectYear(Guid.NewGuid());
		progress.FurthestStep.ShouldBe(3); progress.DivisionId.ShouldBeNull();
		Should.Throw<BusinessException>(() => progress.SaveDraft(5, "{}"));
	}
	[Fact]
	public void Completion_records_the_operator_once_and_locks_drafts()
	{
		var progress = Prepared(); var operatorId = Guid.NewGuid(); var time = DateTime.UtcNow;
		progress.Complete(time, operatorId); progress.Complete(time.AddHours(1), Guid.NewGuid());
		progress.CompletedBy.ShouldBe(operatorId); progress.CompletedAt.ShouldBe(time);
		progress.CurrentStep.ShouldBe(6);
		Should.Throw<BusinessException>(() => progress.SaveDraft(1, "{}"))
			.Code.ShouldBe(InitialSetupErrorCodes.AlreadyCompleted);
	}
	private static InstituteSetupProgress Prepared()
	{
		var progress = new InstituteSetupProgress(Guid.NewGuid());
		progress.Advance(0); progress.Advance(1); progress.SelectYear(Guid.NewGuid());
		progress.Advance(2); progress.SelectDivision(Guid.NewGuid()); progress.Advance(3); progress.Advance(4);
		return progress;
	}
}
