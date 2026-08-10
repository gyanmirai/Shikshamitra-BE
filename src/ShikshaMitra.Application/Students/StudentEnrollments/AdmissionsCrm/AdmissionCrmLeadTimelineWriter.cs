using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadTimelineWriter(
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	internal async Task AddTimelineEventAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details,
		Guid? relatedEntityId,
		bool autoSave = true)
	{
		await AddTimelineEventAsync(
			lead.Id,
			lead.AcademicYearId,
			eventType,
			title,
			details,
			relatedEntityId,
			autoSave);
	}

	internal async Task AddTimelineEventAsync(
		Guid leadId,
		Guid academicYearId,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details,
		Guid? relatedEntityId,
		bool autoSave = true)
	{
		await timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				leadId,
				academicYearId,
				eventType,
				title,
				details,
				relatedEntityId,
				CurrentUser.Id,
				new DateTimeOffset(Clock.Now)),
			autoSave);
	}
}