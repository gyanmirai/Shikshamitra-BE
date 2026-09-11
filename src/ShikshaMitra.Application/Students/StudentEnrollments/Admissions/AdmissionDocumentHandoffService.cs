using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Links canonical evidence in the same unit of work as admission finalization.</summary>
[RemoteService(false)]
public class AdmissionDocumentHandoffService(
	IRepository<AdmissionDocumentEvidence, Guid> evidenceRepository,
	AdmissionDocumentRuntimeService documentRuntime) : ApplicationService, ITransientDependency
{
	public Task EnsureCompleteAsync(AdmissionApplication application) =>
		documentRuntime.EnsureApplicationCompleteAsync(application);

	public async Task LinkAsync(AdmissionApplication application, Student student)
	{
		if (application.TenantId != CurrentTenant.Id || student.TenantId != CurrentTenant.Id)
			throw new EntityNotFoundException(typeof(AdmissionApplication), application.Id);
		if (application.Status != AdmissionApplicationStatus.AdmissionFinalized || application.StudentId != student.Id)
			throw new BusinessException(AdmissionDocumentErrorCodes.InvalidEvidence, "Only finalized evidence can be linked to its Student.");

		var evidence = await evidenceRepository.GetListAsync(item => item.ApplicationId == application.Id);
		if (evidence.Any(item => item.StudentId.HasValue && item.StudentId != student.Id))
			throw new BusinessException(AdmissionDocumentErrorCodes.InvalidEvidence, "Document evidence is already linked to a different Student.");
		var linkedAt = DateTime.UtcNow;
		foreach (var item in evidence.Where(item => !item.StudentId.HasValue))
		{
			item.LinkToStudent(student.Id, CurrentUser.GetId(), linkedAt);
			await evidenceRepository.UpdateAsync(item, autoSave: false);
		}
	}
}
