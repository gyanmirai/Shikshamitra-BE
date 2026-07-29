using System;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmApplicantProgressProjector_Tests
{
	private readonly AdmissionCrmApplicantProgressProjector _projector = new();

	[Fact]
	public void Required_exam_blocks_registration_until_scheduled()
	{
		var result = Project(CreateLead(), entranceExamRequired: true);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.NewInquiry);
		result.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Exam)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.NotStarted);
		result.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Registration)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.Blocked);
		result.NextActionCode.ShouldBe("ScheduleExam");
	}

	[Fact]
	public void Optional_exam_is_explicitly_skipped_and_registration_becomes_next()
	{
		var result = Project(CreateLead(), entranceExamRequired: false);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.NewInquiry);
		result.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Exam)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.Skipped);
		result.CurrentStep.ShouldBe(AdmissionCrmApplicantStepKey.Registration);
		result.NextActionCode.ShouldBe("StartRegistration");
	}

	[Fact]
	public void Eligible_exam_is_visible_before_registration_starts()
	{
		var lead = CreateLead();
		var now = DateTime.UtcNow;
		var slot = new AdmissionCrmExamSlot(
			Guid.NewGuid(),
			lead.TenantId,
			lead.AcademicYearId,
			null,
			null,
			null,
			"Grade 1 entrance exam",
			null,
			"Room 1",
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
			30,
			1);
		var exam = new AdmissionCrmEntranceExamRecord(
			Guid.NewGuid(),
			lead.TenantId,
			lead.AcademicYearId,
			lead.Id,
			slot,
			false,
			null,
			now);
		exam.RecordResult(100, 40, 75, null, now.AddMinutes(1));

		var result = _projector.Project(
			lead,
			new AdmissionCrmApplicantProgressRules(
				true,
				false,
				AdmissionCrmPlacementTiming.StandardBeforeActivation),
			exam,
			null,
			null,
			null,
			null,
			false);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.Eligible);
		result.NextActionCode.ShouldBe("StartRegistration");
	}

	[Fact]
	public void Required_finance_gate_is_projected_after_verified_registration()
	{
		var lead = CreateLead();
		var now = DateTime.UtcNow;
		var registration = new AdmissionCrmRegistrationApplication(
			Guid.NewGuid(),
			lead.TenantId,
			lead.AcademicYearId,
			lead.Id,
			Guid.NewGuid(),
			1,
			null,
			"REG-1",
			"{}",
			"{}",
			now,
			now.AddDays(10),
			0);
		registration.Submit("{}", now.AddMinutes(1), false);
		registration.Verify(now.AddMinutes(2));

		var result = _projector.Project(
			lead,
			new AdmissionCrmApplicantProgressRules(
				false,
				true,
				AdmissionCrmPlacementTiming.StandardBeforeActivation),
			null,
			registration,
			null,
			null,
			null,
			false);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.FinanceVerification);
		result.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Finance)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.NotStarted);
		result.NextActionCode.ShouldBe("RequestFinanceVerification");
	}

	[Fact]
	public void Verified_registration_with_optional_finance_is_registration_done()
	{
		var lead = CreateLead();
		var now = DateTime.UtcNow;
		var registration = new AdmissionCrmRegistrationApplication(
			Guid.NewGuid(),
			lead.TenantId,
			lead.AcademicYearId,
			lead.Id,
			Guid.NewGuid(),
			1,
			null,
			"REG-2",
			"{}",
			"{}",
			now,
			now.AddDays(10),
			0);
		registration.Submit("{}", now.AddMinutes(1), false);
		registration.Verify(now.AddMinutes(2));

		var result = _projector.Project(
			lead,
			new AdmissionCrmApplicantProgressRules(
				false,
				false,
				AdmissionCrmPlacementTiming.StandardBeforeActivation),
			null,
			registration,
			null,
			null,
			null,
			false);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.RegistrationDone);
		result.NextActionCode.ShouldBe("ReviewPlacement");
	}

	[Fact]
	public void Linked_student_is_always_projected_as_activated()
	{
		var lead = CreateLead();
		lead.LinkStudent(Guid.NewGuid());

		var result = Project(lead, entranceExamRequired: false);

		result.PipelineColumn.ShouldBe(AdmissionCrmPipelineColumnKey.Activated);
		result.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Activation)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.Complete);
	}

	[Fact]
	public void Merged_applicant_is_a_duplicate_closed_outcome()
	{
		var lead = CreateLead();
		lead.MergeInto(Guid.NewGuid(), "Duplicate inquiry.", DateTime.UtcNow);

		var result = Project(lead, entranceExamRequired: true);

		result.ClosedOutcome.ShouldBe(AdmissionCrmClosedOutcome.Duplicate);
	}

	private AdmissionCrmApplicantProgressDto Project(
		AdmissionInquiryLead lead,
		bool entranceExamRequired)
	{
		return _projector.Project(
			lead,
			new AdmissionCrmApplicantProgressRules(
				entranceExamRequired,
				false,
				AdmissionCrmPlacementTiming.StandardBeforeActivation),
			null,
			null,
			null,
			null,
			null,
			false);
	}

	private static AdmissionInquiryLead CreateLead()
	{
		return new AdmissionInquiryLead(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			null,
			"Walk-in",
			AdmissionInquiryChannel.WalkInReception,
			null,
			null,
			"en",
			"Aarav",
			null,
			"Patel",
			null,
			"Nisha Patel",
			"9876543210",
			null,
			"Fresh admission",
			Guid.NewGuid(),
			"Grade 1",
			null,
			DateTimeOffset.UtcNow);
	}
}
