using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentWorkflowStageHandler_Tests
{
	private readonly AdmissionWorkflowStageHandlerRegistry _registry = new(
		[new AssessmentAdmissionWorkflowStageHandler()]);

	[Fact]
	public async Task Assessment_Should_Complete_Before_The_Detailed_Form_Is_Submitted()
	{
		var context = Context(AdmissionApplicationStatus.Draft);

		await Should.NotThrowAsync(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Assessment, context));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Submitted)]
	[InlineData(AdmissionApplicationStatus.InReview)]
	[InlineData(AdmissionApplicationStatus.Approved)]
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	public async Task Assessment_Should_Remain_Compatible_With_Post_Submission_Workflows(
		AdmissionApplicationStatus status)
	{
		await Should.NotThrowAsync(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Assessment, Context(status)));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Withdrawn)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	[InlineData(AdmissionApplicationStatus.OfferDeclined)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized)]
	public async Task Assessment_Should_Not_Complete_For_Closed_Applications(
		AdmissionApplicationStatus status)
	{
		await Should.ThrowAsync<BusinessException>(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Assessment, Context(status)));
	}

	[Fact]
	public void Assessment_Metadata_Should_Report_The_Runtime_As_Ready()
	{
		AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Assessment)!
			.IsRuntimeReady.ShouldBeTrue();
	}

	private static AdmissionWorkflowStageRuntimeContext Context(AdmissionApplicationStatus status) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status);
}
