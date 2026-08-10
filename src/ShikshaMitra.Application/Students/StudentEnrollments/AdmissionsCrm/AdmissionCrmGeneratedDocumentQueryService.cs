using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmGeneratedDocumentQueryService(
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmGeneratedDocumentAvailabilityReader availabilityReader,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository)
	: ApplicationService, IAdmissionCrmGeneratedDocumentQueryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId)
	{
		var lead = await leadAccessReader.GetAsync(leadId, nameof(GetAvailabilityAsync));
		return await availabilityReader.GetAsync(lead);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAudit)]
	public async Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId)
	{
		var lead = await leadAccessReader.GetAsync(leadId, nameof(GetHistoryAsync));
		var queryable = await timelineRepository.GetQueryableAsync();
		var events = await AsyncExecuter.ToListAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id &&
			item.LeadId == lead.Id &&
			item.EventType == AdmissionCrmTimelineEventType.GeneratedDocumentIssued));
		return events.OrderByDescending(item => item.OccurredAt).Select(MapHistory).ToList();
	}

	private static AdmissionCrmGeneratedDocumentHistoryDto MapHistory(AdmissionCrmTimelineEvent item)
	{
		var details = item.Details ?? string.Empty;
		var documentType = Enum.GetValues<AdmissionCrmGeneratedDocumentType>()
			.FirstOrDefault(value => details.Contains(
				AdmissionCrmGeneratedDocumentText.Slug(value),
				StringComparison.OrdinalIgnoreCase));
		var language = details.Contains("Language: gu", StringComparison.OrdinalIgnoreCase) ? "gu" :
			details.Contains("Language: hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
		var fileName = details.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "generated-document";
		return new AdmissionCrmGeneratedDocumentHistoryDto
		{
			TimelineEventId = item.Id,
			LeadId = item.LeadId,
			DocumentType = documentType,
			DocumentTypeName = AdmissionCrmGeneratedDocumentText.TypeName(documentType, language),
			FileName = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".pdf",
			Language = language,
			Reason = details,
			GeneratedAt = item.OccurredAt,
			ActorUserId = item.ActorUserId
		};
	}
}
