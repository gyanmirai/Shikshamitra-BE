using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormDefinition_Tests
{
	[Fact]
	public void FormInstanceTracksDraftAndSubmissionState()
	{
		var instance = new AdmissionFormInstance(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 1);

		instance.Status.ShouldBe(AdmissionFormInstanceStatus.Draft);
		instance.MarkSaved();
		instance.Submit(null);
		instance.Status.ShouldBe(AdmissionFormInstanceStatus.Submitted);
		instance.SubmittedAt.ShouldNotBeNull();
		Should.Throw<BusinessException>(() => instance.MarkSaved());
		instance.Reopen();
		instance.Status.ShouldBe(AdmissionFormInstanceStatus.Reopened);
	}

	[Fact]
	public void SectionStoresItsStepperGrouping()
	{
		var stepId = Guid.NewGuid();
		var section = new AdmissionFormSectionDefinition(
			Guid.NewGuid(), Guid.NewGuid(), "personal", "Personal information", 0, null,
			stepId, "applicant", "Applicant details", 1, "{\"operator\":\"ALL\"}");

		section.Code.ShouldBe("PERSONAL");
		section.StepId.ShouldBe(stepId);
		section.StepCode.ShouldBe("APPLICANT");
		section.StepName.ShouldBe("Applicant details");
		section.StepDisplayOrder.ShouldBe(1);
	}
    [Fact]
    public void NewFormIsDraftAndNotDeleted()
    {
        var form = new AdmissionFormDefinition(
            Guid.NewGuid(), null, " Student admission ", " student_admission ",
            AdmissionFormLayoutType.SinglePage, description: "Applicant details");

        form.Status.ShouldBe(AdmissionFormStatus.Draft);
        form.IsDeleted.ShouldBeFalse();
        form.Code.ShouldBe("STUDENT_ADMISSION");
        form.Description.ShouldBe("Applicant details");
    }

    [Fact]
    public void FormPurposeIsStoredAndCanChangeOnlyWhileDraft()
    {
        var form = new AdmissionFormDefinition(
            Guid.NewGuid(), null, "Family inquiry", "FAMILY_INQUIRY",
            AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);

        form.Purpose.ShouldBe(AdmissionFormPurpose.Inquiry);
        form.UpdateDetails(
            "Parent consent", "PARENT_CONSENT", AdmissionFormLayoutType.SinglePage,
            purpose: AdmissionFormPurpose.Consent);
        form.Purpose.ShouldBe(AdmissionFormPurpose.Consent);

        form.Publish(null);
        Should.Throw<BusinessException>(() => form.UpdateDetails(
            "Correction", "CORRECTION", AdmissionFormLayoutType.SinglePage,
            purpose: AdmissionFormPurpose.Correction));
    }

    [Fact]
    public void PublishedFormCannotBeEditedAndDraftCannotBeArchived()
    {
        var form = new AdmissionFormDefinition(
            Guid.NewGuid(), null, "Student admission", "STUDENT_ADMISSION", AdmissionFormLayoutType.SinglePage);

        Should.Throw<BusinessException>(() => form.Archive());

        form.Publish(null);
        Should.Throw<BusinessException>(() => form.UpdateDetails(
            "Changed", "CHANGED", AdmissionFormLayoutType.Stepper));

        form.Archive();
        form.Status.ShouldBe(AdmissionFormStatus.Archived);
    }

    [Fact]
    public void FieldStoresChoiceOptions()
    {
        var options = "[{\"value\":\"male\",\"label\":\"Male\"}]";
        var field = new AdmissionFormFieldDefinition(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "gender", "Gender",
            AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, null,
            required: true, readOnly: false, displayOrder: 0, placeholder: null,
            helpText: null, defaultValue: null, validationJson: null,
            visibilityRuleJson: null, optionsJson: options);

        field.OptionsJson.ShouldBe(options);
    }
}
