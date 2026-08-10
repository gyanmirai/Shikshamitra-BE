using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicLanguageReader(ITenantRepository tenantRepository) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<string>> GetAsync(Guid tenantId)
	{
		using (CurrentTenant.Change(null))
		{
			var tenant = await tenantRepository.FindAsync(tenantId);
			return AdmissionCrmPublicBranding.ParseSupportedLanguages(
				tenant?.GetProperty<string>("AdmissionsSupportedLanguages"));
		}
	}
}
