using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookAccess(
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	StudentScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public Task EnsureYearAsync(Guid academicYearId, string operation) => scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
	public Task EnsureConfigurationAsync(Guid academicYearId, string operation) => scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, operation);

	public async Task<AdmissionCrmPlaybook> GetAsync(Guid id, string operation)
	{
		var playbook = await playbookRepository.GetAsync(id);
		if (playbook.TenantId != CurrentTenant.Id)
			throw new BusinessException("AdmissionCrm:PlaybookNotFound").WithData("PlaybookId", id);
		await EnsureYearAsync(playbook.AcademicYearId, operation);
		return playbook;
	}

	public static void EnsurePublished(AdmissionCrmPlaybook playbook)
	{
		if (playbook.Status != AdmissionCrmPlaybookStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedPlaybookRequired").WithData("PlaybookId", playbook.Id);
	}
}
