using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionWorkflowDefinition_Tests
{
	[Fact]
	public void NewWorkflowIsDraftAndNormalizesCode()
	{
		var workflow = new AdmissionWorkflowDefinition(
			Guid.NewGuid(), null, "Standard admission", " standard_v1 ", description: "Grade 5 intake");
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		workflow.IsDeleted.ShouldBeFalse();
		workflow.Code.ShouldBe("STANDARD_V1");
		workflow.Description.ShouldBe("Grade 5 intake");
	}

	[Fact]
	public void PublishedWorkflowCannotBeEdited()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		workflow.Publish(null);
		Should.Throw<BusinessException>(() => workflow.UpdateDetails("Changed", "CHANGED"));
	}

	[Fact]
	public void PublishedWorkflowCanBeArchivedButDraftCannot()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		Should.Throw<BusinessException>(() => workflow.Archive());

		workflow.Publish(null);
		workflow.Archive();

		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Archived);
	}
}
