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
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", " standard_v1 ");
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		workflow.Code.ShouldBe("STANDARD_V1");
	}

	[Fact]
	public void PublishedWorkflowCannotBeEdited()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		workflow.Publish(null);
		Should.Throw<BusinessException>(() => workflow.UpdateDetails("Changed", "CHANGED"));
	}
}
