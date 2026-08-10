using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSupportSessionWorkflow(
	IRepository<AdmissionCrmPlatformSupportSession, Guid> repository) : ApplicationService, ITransientDependency
{
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync(StartAdmissionCrmSupportSessionDto input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		var now = Clock.Now;
		var session = new AdmissionCrmPlatformSupportSession(GuidGenerator.Create(), CurrentUser.GetId(), input.TenantId, input.Reason, input.Reference, now.AddMinutes(30));
		await repository.InsertAsync(session, autoSave: true);
		return AdmissionCrmSetupFormat.ToDto(session, now);
	}

	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		return AdmissionCrmSetupFormat.ToDto(await GetOwnedSessionAsync(id, null), Clock.Now);
	}

	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task EndSupportSessionAsync(Guid id)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		var session = await GetOwnedSessionAsync(id, null);
		session.End(Clock.Now);
		await repository.UpdateAsync(session, autoSave: true);
	}

	internal async Task<AdmissionCrmPlatformSupportSession> GetOwnedSessionAsync(Guid id, Guid? targetTenantId)
	{
		var session = await repository.GetAsync(id);
		if (session.OperatorUserId != CurrentUser.GetId() || (targetTenantId.HasValue && session.TargetTenantId != targetTenantId.Value) || !session.IsActive(Clock.Now)) throw new BusinessException("AdmissionCrm:InvalidOrExpiredSupportSession");
		return session;
	}
}