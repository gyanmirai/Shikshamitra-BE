using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceAuditEventManager(
	IRepository<PerformanceAuditEvent, Guid> auditRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant)
	: DomainService
{
	public virtual async Task RecordAsync(
		Guid? reviewId,
		string eventType,
		string summary,
		CancellationToken cancellationToken = default)
	{
		await auditRepository.InsertAsync(
			new PerformanceAuditEvent(guidGenerator.Create(), currentTenant.Id, reviewId, eventType, summary),
			autoSave: true,
			cancellationToken: cancellationToken);
	}
}
