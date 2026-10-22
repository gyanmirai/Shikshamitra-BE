using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormStageCommandService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionFormInstance, Guid> forms,
	IRepository<AdmissionFormSectionDefinition, Guid> sections,
	IRepository<AdmissionFormSubmission, Guid> submissions,
	IRepository<AdmissionFormChangeHistory, Guid> history,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionFormStageAccessService access,
	AdmissionFormStageReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionFormStageDto> VerifySectionAsync(
		Guid applicationId, Guid stageId, Guid sectionId, VerifyAdmissionFormSectionDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId, requirePreparation: true);
		AdmissionFormStageAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (context.Form.Status != AdmissionFormInstanceStatus.Submitted || context.Policy?.RequireSectionVerification != true)
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState,
				"Submit the form before recording section verification.");
		var section = await sections.FindAsync(sectionId);
		if (section?.FormDefinitionId != context.Form.FormDefinitionId ||
			!context.Policy.VerificationSectionCodes.Contains(section.Code, StringComparer.OrdinalIgnoreCase))
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidSection,
				"Choose a section required by this Form stage.");
		var reason = NormalizeReason(input.Reason, required: !input.Verified);
		await history.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id,
			context.Form.Id, applicationId, "SectionVerification", JsonSerializer.Serialize(new
			{
				SectionCode = section.Code,
				input.Verified,
				Reason = reason
			}), CurrentUser.Id), false);
		await applications.UpdateAsync(context.Application, false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(input.Verified ? "AdmissionFormSectionVerified" : "AdmissionFormSectionVerificationRevoked",
			input.Verified ? $"Form section verified: {section.Name}" : $"Form section verification revoked: {section.Name}",
			context.Application.ApplicantId, applicationId,
			metadataJson: JsonSerializer.Serialize(new { StageId = stageId, SectionId = section.Id, Reason = reason })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(applicationId, stageId);
	}

	public async Task<AdmissionFormStageDto> RequestCorrectionAsync(
		Guid applicationId, Guid stageId, RequestAdmissionFormCorrectionDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId, requirePreparation: true);
		AdmissionFormStageAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (context.Policy?.AllowCorrectionRequests != true || context.Form.Status != AdmissionFormInstanceStatus.Submitted)
			throw new BusinessException(AdmissionFormStageErrorCodes.CorrectionNotAllowed,
				"This Form stage does not allow a correction request now.");
		var attempts = await submissions.CountAsync(item => item.FormInstanceId == context.Form.Id);
		if (attempts >= context.Policy.MaxSubmissionAttempts)
			throw new BusinessException(AdmissionFormStageErrorCodes.AttemptLimitReached,
				$"The limit of {context.Policy.MaxSubmissionAttempts} submission attempts has been reached.");
		var reason = NormalizeReason(input.Reason, required: true)!;
		context.Application.Reopen();
		context.Form.Reopen();
		await applications.UpdateAsync(context.Application, false);
		await forms.UpdateAsync(context.Form, false);
		await history.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id,
			context.Form.Id, applicationId, "CorrectionRequested",
			JsonSerializer.Serialize(new { Reason = reason }), CurrentUser.Id), false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("AdmissionFormCorrectionRequested", "Application form returned for correction",
			context.Application.ApplicantId, applicationId,
			metadataJson: JsonSerializer.Serialize(new { StageId = stageId, Reason = reason })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(applicationId, stageId);
	}

	private static string? NormalizeReason(string? value, bool required)
	{
		var reason = value?.Trim();
		if (required && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState, "Enter a reason.");
		if (reason?.Length > 512)
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState, "Reasons allow up to 512 characters.");
		return string.IsNullOrWhiteSpace(reason) ? null : reason;
	}
}
