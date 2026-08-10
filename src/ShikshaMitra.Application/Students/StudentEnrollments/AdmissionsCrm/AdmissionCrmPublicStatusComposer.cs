using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicStatusComposer(
	AdmissionCrmPublicStatusCoreReader coreReader,
	AdmissionCrmPublicChecklistReader checklistReader,
	AdmissionCrmPublicActivityReader activityReader,
	AdmissionCrmPublicRegistrationWorkflow registrationWorkflow,
	AdmissionCrmPublicOfferWorkflow offerWorkflow,
	AdmissionCrmPublicCommunicationPreferences preferences,
	AdmissionCrmDocumentApplicantWorkflow documentWorkflow,
	AdmissionCrmPublicLanguageReader languageReader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicantStatusDto> ComposeAsync(
		AdmissionInquiryLead lead,
		string? requestedLanguage = null)
	{
		var exam = await coreReader.GetExamAsync(lead.Id);
		var registration = await registrationWorkflow.GetApplicationOrDefaultAsync(lead.Id);
		var offer = await offerWorkflow.GetOfferOrDefaultAsync(lead.Id);
		var activation = await coreReader.GetActivationAsync(lead.Id);
		await offerWorkflow.OpenIfNeededAsync(lead, offer);
		await registrationWorkflow.OpenIfNeededAsync(lead, registration);

		var status = new AdmissionApplicantStatusDto
		{
			LeadId = lead.Id,
			DisplayName = AdmissionCrmPublicProgressMapper.BuildDisplayName(lead),
			Stage = lead.Stage,
			StageName = await coreReader.GetStageNameAsync(lead.AcademicYearId, lead.Stage),
			LastActivityTime = lead.LastActivityTime,
			NextStep = AdmissionCrmPublicProgressMapper.GetNextStep(lead.Stage),
			Checklist = AdmissionCrmPublicProgressMapper.BuildChecklist(lead, exam),
			ApplicantChecklist = await checklistReader.GetAsync(lead),
			Activity = await activityReader.GetAsync(lead),
			Exam = exam == null ? null : AdmissionCrmPublicProgressMapper.ToExamDto(exam),
			Registration = registration == null ? null : AdmissionCrmPublicRegistrationMapper.ToDto(registration),
			Documents = await documentWorkflow.GetChecklistAsync(lead.Id),
			Offer = offer == null ? null : AdmissionCrmPublicOfferWorkflow.ToDto(offer),
			FinancialStatus = await coreReader.GetFinancialStatusAsync(lead.Id),
			AdmissionRoute = await coreReader.GetRouteAsync(lead.Id),
			Activation = activation == null ? null : AdmissionCrmPublicStatusCoreReader.ToActivationDto(activation),
			CommunicationPreferences = await preferences.GetAsync(lead),
			CanWithdrawApplication = CanWithdraw(lead, activation)
		};
		return AdmissionCrmPublicLocalization.Apply(
			status,
			requestedLanguage ?? lead.PreferredCommunicationLanguage,
			await languageReader.GetAsync(CurrentTenant.Id!.Value));
	}

	private static bool CanWithdraw(AdmissionInquiryLead lead, AdmissionCrmActivation? activation) =>
		lead.StudentId == null &&
		lead.Stage is not (AdmissionInquiryStage.Archived or AdmissionInquiryStage.Rejected) &&
		(activation == null || activation.Status == AdmissionCrmActivationStatus.Cancelled);
}
