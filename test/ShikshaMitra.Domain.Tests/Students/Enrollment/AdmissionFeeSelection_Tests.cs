using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeSelection_Tests
{
	[Fact]
	public void Snapshot_is_application_scoped_and_survives_roundtrip_without_creating_student_fees()
	{
		var application = Application(); var other = Application(); var selection = Selection();
		application.SaveFeeSelection(selection, false);
		application.GetFeeSelection()!.Heads[0].ConfiguredAmount.ShouldBe(200m);
		application.GetFeeSelection()!.StructureId.ShouldBe(selection.StructureId);
		other.GetFeeSelection().ShouldBeNull(); application.StudentId.ShouldBeNull();
	}

	[Fact]
	public void Payment_evidence_blocks_replacement_even_if_payment_later_reversed()
	{
		var application = Application(); var original = Selection();
		application.SaveFeeSelection(original, false);
		Should.Throw<BusinessException>(() => application.SaveFeeSelection(Selection(), true)).Code
			.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		application.GetFeeSelection()!.StructureId.ShouldBe(original.StructureId);
	}

	[Fact]
	public void Confirmation_requires_linked_onboarding_and_is_idempotent()
	{
		var application = Application(); application.SaveFeeSelection(Selection(), false);
		Should.Throw<BusinessException>(() => application.ConfirmOnboardingFeeSelection(Guid.NewGuid(), DateTimeOffset.UtcNow))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidState);
		application.Submit(); application.Approve(Guid.NewGuid(), null);
		application.FinalizeApprovedDirectAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
		var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
		application.ConfirmOnboardingFeeSelection(actor, now);
		application.ConfirmOnboardingFeeSelection(Guid.NewGuid(), now.AddHours(1));
		application.GetFeeSelection()!.ConfirmedBy.ShouldBe(actor);
		application.GetFeeSelection()!.OnboardingConfirmedAt.ShouldBe(now);
		Should.Throw<BusinessException>(() => application.SaveFeeSelection(Selection(), false)).Code
			.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidState);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1.111)]
	public void Invalid_reference_amounts_are_rejected(decimal amount)
	{
		var selection = Selection();
		Should.Throw<BusinessException>(() => Application().SaveFeeSelection(selection with
		{ Heads = [selection.Heads[0] with { ConfiguredAmount = amount }] }, false)).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidSelection);
	}

	[Fact]
	public void Duplicate_heads_and_empty_selections_are_rejected()
	{
		var selection = Selection();
		Should.Throw<BusinessException>(() => Application().SaveFeeSelection(selection with { Heads = [] }, false));
		Should.Throw<BusinessException>(() => Application().SaveFeeSelection(selection with { Heads = [selection.Heads[0], selection.Heads[0]] }, false));
	}

	private static AdmissionApplication Application() => new(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	private static AdmissionFeeSelectionSnapshot Selection() => new(Guid.NewGuid(), "Published plan", 2,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INR",
		[new(Guid.NewGuid(), "Form fee", 200m, false, true, Guid.NewGuid(), Guid.NewGuid())], Guid.NewGuid(), DateTimeOffset.UtcNow);
}
