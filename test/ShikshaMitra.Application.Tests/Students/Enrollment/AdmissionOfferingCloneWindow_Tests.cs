using System;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionTrack.General, AdmissionEntryMode.Inquiry)]
	[InlineData(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration)]
	public Task Offering_clone_inherits_target_cycle_window_and_preserves_source(AdmissionTrack track, AdmissionEntryMode entryMode) => RunCertificationScenarioAsync(async () =>
	{
		var source = await CreateSpecialRegistrationOfferingAsync(track, entryMode);
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var sourceDto = await service.GetAsync(source.Id);
		var sourceCycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(source.AdmissionCycleId));
		var customOpen = new DateTimeOffset(DateTime.SpecifyKind(sourceCycle.StartDate.AddDays(5), DateTimeKind.Utc));
		var customClose = new DateTimeOffset(DateTime.SpecifyKind(sourceCycle.StartDate.AddDays(20), DateTimeKind.Utc));
		// Generic update compatibility remains available; only cloning stops copying these overrides.
		var savedSource = await service.UpdateAsync(source.Id, new()
		{
			Name = sourceDto.Name, Code = sourceDto.Code, Track = sourceDto.Track, EntryMode = sourceDto.EntryMode,
			CampusId = sourceDto.CampusId, BoardId = sourceDto.BoardId, MediumId = sourceDto.MediumId,
			StageId = sourceDto.StageId, StandardId = sourceDto.StandardId, IsActive = sourceDto.IsActive,
			UseCycleDates = false, OpenDate = customOpen, CloseDate = customClose,
			WorkflowDefinitionId = sourceDto.WorkflowDefinitionId, InquiryFormDefinitionId = sourceDto.InquiryFormDefinitionId,
			ApplicationFormDefinitionId = sourceDto.ApplicationFormDefinitionId,
			InquiryIntakeProfileId = sourceDto.InquiryIntakeProfileId, CounsellorAssignmentProfileId = sourceDto.CounsellorAssignmentProfileId,
			ConcurrencyStamp = sourceDto.ConcurrencyStamp
		});
		savedSource.UseCycleDates.ShouldBeFalse();
		savedSource.OpenDate.ShouldBe(customOpen);
		savedSource.CloseDate.ShouldBe(customClose);

		var targetCycleId = await CreateEligibilityOfferingCycleAsync(source.AdmissionCycleId);
		var targetCycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(targetCycleId));
		var clone = await service.CloneAsync(source.Id, new()
		{
			AdmissionCycleId = targetCycleId, CampusId = source.CampusId, BoardId = source.BoardId,
			MediumId = source.MediumId, StageId = source.StageId, StandardId = source.StandardId,
			Code = "CLONED-CYCLE-DATES"
		});
		clone.UseCycleDates.ShouldBeTrue();
		clone.OpenDate.ShouldBeNull();
		clone.CloseDate.ShouldBeNull();
		clone.EffectiveOpenDate.Date.ShouldBe(targetCycle.StartDate.Date);
		clone.EffectiveCloseDate.Date.ShouldBe(targetCycle.EndDate.Date);
		clone.Track.ShouldBe(savedSource.Track);
		clone.EntryMode.ShouldBe(savedSource.EntryMode);
		clone.WorkflowDefinitionId.ShouldBe(savedSource.WorkflowDefinitionId);
		clone.InquiryFormDefinitionId.ShouldBe(savedSource.InquiryFormDefinitionId);
		clone.ApplicationFormDefinitionId.ShouldBe(savedSource.ApplicationFormDefinitionId);
		clone.InquiryIntakeProfileId.ShouldBe(savedSource.InquiryIntakeProfileId);
		clone.CounsellorAssignmentProfileId.ShouldBe(savedSource.CounsellorAssignmentProfileId);
		clone.IsActive.ShouldBe(savedSource.IsActive);

		var cycleService = GetRequiredService<IAdmissionCycleAppService>();
		var currentTargetCycle = await cycleService.GetAsync(targetCycleId);
		var revisedCycle = await cycleService.UpdateWindowAsync(targetCycleId, new()
		{
			ConcurrencyStamp = currentTargetCycle.ConcurrencyStamp, Reason = "Revise the cloned intake cycle window",
			StartDate = currentTargetCycle.StartDate.AddDays(30), EndDate = currentTargetCycle.EndDate.AddDays(-30)
		});
		var refreshedClone = await service.GetAsync(clone.Id);
		refreshedClone.UseCycleDates.ShouldBeTrue();
		refreshedClone.OpenDate.ShouldBeNull();
		refreshedClone.CloseDate.ShouldBeNull();
		refreshedClone.EffectiveOpenDate.Date.ShouldBe(revisedCycle.StartDate.Date);
		refreshedClone.EffectiveCloseDate.Date.ShouldBe(revisedCycle.EndDate.Date);
		var unchangedSource = await service.GetAsync(source.Id);
		unchangedSource.UseCycleDates.ShouldBeFalse();
		unchangedSource.OpenDate.ShouldBe(customOpen);
		unchangedSource.CloseDate.ShouldBe(customClose);
		unchangedSource.ConcurrencyStamp.ShouldBe(savedSource.ConcurrencyStamp);
	});
}
