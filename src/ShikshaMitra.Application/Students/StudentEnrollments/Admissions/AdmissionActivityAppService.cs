using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionActivityAppService(IRepository<AdmissionActivity, Guid> repository,
	AdmissionApplicationReadService applicationReadService,
	AdmissionApplicantScopeAccessService applicantScope) : ApplicationService, IAdmissionActivityAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<System.Collections.Generic.List<AdmissionActivityDto>> GetForApplicantAsync(Guid applicantId)
	{
		var query = await applicantScope.ApplyActivityScopeAsync(await repository.GetQueryableAsync(), applicantId);
		return await WithActorsAsync((await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.OccurredAt))).Select(Map).ToList());
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<System.Collections.Generic.List<AdmissionActivityDto>> GetForApplicationAsync(Guid applicationId)
	{
		await applicationReadService.GetEntityAsync(applicationId);
		var result = (await repository.GetListAsync(x => x.ApplicationId == applicationId)).OrderByDescending(x => x.OccurredAt).Select(Map).ToList();
		foreach (var activity in result) activity.OccurredAt = AdmissionOfferUtcTime.Normalize(activity.OccurredAt);
		return await WithActorsAsync(result);
	}
	private async Task<System.Collections.Generic.List<AdmissionActivityDto>> WithActorsAsync(System.Collections.Generic.List<AdmissionActivityDto> items)
	{
		var names = await LazyServiceProvider.LazyGetRequiredService<StudentJourneyActorReader>().ReadAsync(items.Select(item => item.CreatorId));
		foreach (var item in items) item.ActorName ??= StudentJourneyActorReader.Name(names, item.CreatorId);
		return items;
	}
	private static AdmissionActivityDto Map(AdmissionActivity x) => new() { Id = x.Id, ApplicantId = x.ApplicantId, ApplicationId = x.ApplicationId, InquiryId = x.InquiryId, ActivityType = x.ActivityType, Description = x.Description, OccurredAt = x.OccurredAt, CreatorId = x.CreatorId, ActorName = StudentJourneyActorMetadata.Name(x.MetadataJson) };
}
