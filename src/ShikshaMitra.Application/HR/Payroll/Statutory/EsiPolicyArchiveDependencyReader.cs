using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Statutory;

// Historical versions and archived templates remain immutable payroll evidence.
public class EsiPolicyArchiveDependencyReader(
	IRepository<SalaryStructureTemplate, Guid> templates,
	IRepository<SalaryStructureTemplateVersion, Guid> versions) : ITransientDependency
{
	public async Task<List<EsiPolicyTemplateDependencyDto>> ReadAsync(Guid policyId)
	{
		var selected = await versions.GetListAsync(x => x.DefaultEsiPolicyId == policyId && x.DefaultEsiManagementMode == EsiManagement.InPlatform);
		var owners = await templates.GetListAsync(x => x.Status != SalaryTemplateStatus.Archived);
		return owners.SelectMany(template => selected
			.Where(version => version.TemplateId == template.Id &&
				(version.VersionNumber == template.PublishedVersion || version.VersionNumber == template.LatestVersion))
			.Select(version => new EsiPolicyTemplateDependencyDto
			{
				TemplateId = template.Id, TemplateName = template.Name,
				Version = version.VersionNumber, Status = version.Status.ToString()
			})).OrderBy(x => x.TemplateName).ThenBy(x => x.Version).ToList();
	}
}
