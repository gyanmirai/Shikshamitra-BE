using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewQueueService(IRepository<AdmissionReviewWorkItem, Guid> workItems,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicant, Guid> applicants, IRepository<AdmissionCycle, Guid> cycles,
	AdmissionApplicationScopeAccessService scope,
	AdmissionReviewReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionReviewQueueItemDto>> GetAsync(AdmissionReviewQueueInput input)
	{
		var visible = await scope.GetScopedOfferingsAsync();
		var workQuery = await workItems.GetQueryableAsync(); var applicationQuery = await scope.GetScopedApplicationsAsync();
		var stageQuery = await stages.GetQueryableAsync(); var applicantQuery = await applicants.GetQueryableAsync();
		var cycleQuery = await cycles.GetQueryableAsync();
		var now = DateTime.UtcNow;
		var query = from work in workQuery
			join application in applicationQuery on work.ApplicationId equals application.Id
			join stage in stageQuery on work.StageInstanceId equals stage.Id
			join applicant in applicantQuery on application.ApplicantId equals applicant.Id
			where stage.Status == AdmissionRuntimeStageStatus.Active && application.CurrentStageInstanceId == stage.Id &&
				visible.Any(offering => offering.Id == application.AdmissionOfferingId &&
					(!input.BoardId.HasValue || offering.BoardId == input.BoardId.Value) &&
					(!input.MediumId.HasValue || offering.MediumId == input.MediumId.Value) &&
					(!input.StandardId.HasValue || offering.StandardId == input.StandardId.Value) &&
				(input.StandardIds.Count == 0 || Enumerable.Contains(input.StandardIds, offering.StandardId))) &&
				(!input.AcademicYearId.HasValue || cycleQuery.Any(cycle =>
					cycle.Id == application.AdmissionCycleId && cycle.AcademicYearId == input.AcademicYearId.Value)) &&
				(!input.AssignedToMe || work.AssignedReviewerId == CurrentUser.Id) &&
				(!input.OverdueOnly || work.DueAt < now && work.Status != "approved" && work.Status != "waitlisted" && work.Status != "rejected")
			select new { application.Id, StageId = stage.Id, application.ApplicationNumber, ApplicantName = applicant.FirstName + " " + (applicant.LastName ?? ""),
				StageName = stage.Name, work.DueAt, work.CreationTime };
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(item => item.DueAt == null).ThenBy(item => item.DueAt).ThenBy(item => item.CreationTime)
			.Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var result = new List<AdmissionReviewQueueItemDto>();
		foreach (var row in rows)
		{
			var state = await reader.GetAsync(row.Id, row.StageId);
			result.Add(new() { ApplicationId = row.Id, StageId = row.StageId, ApplicationNumber = row.ApplicationNumber,
				ApplicantName = row.ApplicantName.Trim(), StageName = row.StageName, Status = state.Status,
				AssignedReviewerId = state.AssignedReviewerId, AssignedReviewerName = state.AssignedReviewerName,
				DueAt = state.DueAt, IsOverdue = state.IsOverdue, IsStale = state.IsStale });
		}
		return new(count, result);
	}
}
