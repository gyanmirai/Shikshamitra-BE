using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionDocumentWorkflowStageHandler_Tests
{
	[Theory]
	[InlineData(AdmissionApplicationStatus.Draft)]
	[InlineData(AdmissionApplicationStatus.Submitted)]
	[InlineData(AdmissionApplicationStatus.InReview)]
	[InlineData(AdmissionApplicationStatus.Approved)]
	public async Task Documents_allow_open_applications_before_or_after_form_submission(AdmissionApplicationStatus status)
	{
		var registry = new AdmissionWorkflowStageHandlerRegistry([new DocumentVerificationAdmissionWorkflowStageHandler()]);
		await Should.NotThrowAsync(() => registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.DocumentVerification,
			new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status)));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Withdrawn)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	[InlineData(AdmissionApplicationStatus.OfferDeclined)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized)]
	public async Task Documents_reject_closed_applications(AdmissionApplicationStatus status)
	{
		var registry = new AdmissionWorkflowStageHandlerRegistry([new DocumentVerificationAdmissionWorkflowStageHandler()]);
		await Should.ThrowAsync<BusinessException>(() => registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.DocumentVerification,
			new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status)));
	}
}
