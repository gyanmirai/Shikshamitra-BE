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

public class AdmissionCrmSettingsAccessWorkflow(
	IRepository<AdmissionCrmSettingsAccessPolicy, Guid> repository,
	AdmissionCrmSupportSessionWorkflow supportSessions) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync()
	{
		var configured = await repository.GetListAsync();
		return AdmissionCrmSetupFormat.SettingsKeys.Select(key =>
		{
			var policy = configured.FirstOrDefault(x => x.SettingsKey == key && x.SectionKey == null);
			return new AdmissionCrmSettingsAccessDto { SettingsKey = key, AccessMode = policy?.AccessMode ?? AdmissionCrmSettingsAccessMode.Editable };
		}).ToList();
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManageTenantAccess)]
	public async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, Guid supportSessionId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		await supportSessions.GetOwnedSessionAsync(supportSessionId, tenantId);
		return await UpdateAccessPolicyCoreAsync(tenantId, input);
	}

	internal async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyCoreAsync(Guid tenantId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input)
	{
		if (input.Any(x => !AdmissionCrmSetupFormat.SettingsKeys.Contains(x.SettingsKey, StringComparer.OrdinalIgnoreCase))) throw new BusinessException("AdmissionCrm:UnknownSettingsKey");
		using (CurrentTenant.Change(tenantId))
		{
			var existing = await repository.GetListAsync();
			foreach (var item in input)
			{
				var policy = existing.FirstOrDefault(x => x.SettingsKey == item.SettingsKey && x.SectionKey == item.SectionKey);
				if (policy is null) await repository.InsertAsync(new AdmissionCrmSettingsAccessPolicy(GuidGenerator.Create(), tenantId, item.SettingsKey, item.SectionKey, item.AccessMode));
				else { policy.Update(item.SettingsKey, item.SectionKey, item.AccessMode); await repository.UpdateAsync(policy); }
			}
			await CurrentUnitOfWork!.SaveChangesAsync();
			return (await repository.GetListAsync()).Select(x => new AdmissionCrmSettingsAccessDto { SettingsKey = x.SettingsKey, SectionKey = x.SectionKey, AccessMode = x.AccessMode }).ToList();
		}
	}
}