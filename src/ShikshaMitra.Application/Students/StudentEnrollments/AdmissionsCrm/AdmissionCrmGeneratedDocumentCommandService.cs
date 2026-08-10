using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGeneratedDocumentCommandWorkflow(
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmGeneratedDocumentRenderer renderer,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository)
	: ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(
		Guid leadId,
		PreviewAdmissionCrmGeneratedDocumentDto input)
	{
		var lead = await leadAccessReader.GetAsync(leadId, nameof(PreviewAsync));
		var language = AdmissionCrmGeneratedDocumentText.NormalizeLanguage(input.Language);
		return await renderer.RenderAsync(lead, input.DocumentType, language, Clock.Now, true, null);
	}

	public async Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(
		Guid leadId,
		IssueAdmissionCrmGeneratedDocumentDto input)
	{
		var lead = await leadAccessReader.GetAsync(leadId, nameof(IssueAsync));
		await AuthorizationService.CheckAsync(GetRequiredPermission(input.DocumentType));
		var language = AdmissionCrmGeneratedDocumentText.NormalizeLanguage(input.Language);
		var generatedAt = Clock.Now;
		var document = await renderer.RenderAsync(
			lead,
			input.DocumentType,
			language,
			generatedAt,
			false,
			input.Reason);
		var timelineEvent = new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.Id,
			lead.AcademicYearId,
			AdmissionCrmTimelineEventType.GeneratedDocumentIssued,
			$"{document.DocumentTypeName} issued",
			$"{document.FileName}. Language: {language}. Reason: {input.Reason ?? "Operational issue"}.",
			null,
			CurrentUser.Id,
			generatedAt);
		await timelineRepository.InsertAsync(timelineEvent, autoSave: true);
		document.TimelineEventId = timelineEvent.Id;
		return document;
	}

	private static string GetRequiredPermission(AdmissionCrmGeneratedDocumentType documentType) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam,
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement or AdmissionCrmGeneratedDocumentType.CorrectionNotice => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks,
		AdmissionCrmGeneratedDocumentType.OfferLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers,
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation,
		AdmissionCrmGeneratedDocumentType.CancellationLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission,
		_ => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View
	};
}
