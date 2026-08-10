using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsSavedViewWorkflow(
	IRepository<AdmissionCrmOperationsSavedView, Guid> repository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public async Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, CreateAdmissionCrmOperationsSavedViewDto input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateSavedViewAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		var entity = new AdmissionCrmOperationsSavedView(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			userId, input.Name, input.QueueType, JsonSerializer.Serialize(input.Filters), input.IsShared);
		await repository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmOperationsSavedViewMapper.Map(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetSavedViewsAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		var queryable = await repository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (x.IsShared || x.OwnerUserId == userId)));
		return items.OrderBy(x => x.Name).Select(AdmissionCrmOperationsSavedViewMapper.Map).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public async Task DeleteSavedViewAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(DeleteSavedViewAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		if (entity.OwnerUserId != userId && !entity.IsShared) throw new AbpAuthorizationException("Only the saved-view owner can delete this view.");
		await repository.DeleteAsync(entity, autoSave: true);
	}
}

internal static class AdmissionCrmOperationsSavedViewMapper
{
	internal static AdmissionCrmOperationsSavedViewDto Map(AdmissionCrmOperationsSavedView entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		OwnerUserId = entity.OwnerUserId,
		Name = entity.Name,
		QueueType = entity.QueueType,
		IsShared = entity.IsShared,
		Filters = JsonSerializer.Deserialize<GetAdmissionCrmOperationsQueueInput>(entity.FiltersJson) ?? new GetAdmissionCrmOperationsQueueInput()
	};
}