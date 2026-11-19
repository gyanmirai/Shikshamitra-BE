using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionOfferingConfigurationUsageService(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionApplication, Guid> applications,
	IDataFilter filters,
	IAdmissionOfferingConfigurationLock configurationLock) : ITransientDependency
{
	private const string UsageProperty = "AdmissionConfigurationUsed";

	public async Task<bool> HasUsageAsync(AdmissionOffering offering)
	{
		if (offering.GetProperty<bool>(UsageProperty)) return true;
		// Deleting intake evidence must not unlock a previously used setup. Tenant filtering stays enabled.
		using (filters.Disable<ISoftDelete>())
			return await inquiries.AnyAsync(inquiry => inquiry.AdmissionOfferingId == offering.Id) ||
				await applications.AnyAsync(application => application.AdmissionOfferingId == offering.Id);
	}

	public Task<int> GetInquiryCountAsync(Guid offeringId) =>
		inquiries.CountAsync(inquiry => inquiry.AdmissionOfferingId == offeringId);

	public async Task RecordAsync(AdmissionOffering? offering)
	{
		if (offering == null || offering.GetProperty<bool>(UsageProperty)) return;
		var capturedContext = CaptureContext(offering);
		await configurationLock.AcquireAndRefreshAsync(offering);
		if (!Equals(capturedContext, CaptureContext(offering)))
			throw new BusinessException(AdmissionErrorCodes.OfferingConcurrencyConflict,
				"The admission setup changed while intake was starting. Refresh the offering and try again.");
		// Another first intake may already have recorded usage while this request waited for the transaction lock.
		if (offering.GetProperty<bool>(UsageProperty)) return;
		offering.SetProperty(UsageProperty, true);
		// First intake shares the Offering's optimistic concurrency boundary with setup/state changes.
		await offerings.UpdateAsync(offering, autoSave: false);
	}

	public async Task RecordTransferAsync(Guid? previousOfferingId, AdmissionOffering target)
	{
		var source = previousOfferingId.HasValue && previousOfferingId != target.Id
			? await offerings.FindAsync(previousOfferingId.Value) : null;
		// Legacy moves can mark two offerings for the first time. Every transfer uses the same lock order.
		foreach (var offering in new[] { source, target }.OfType<AdmissionOffering>().OrderBy(item => item.Id))
			await RecordAsync(offering);
	}

	private static object CaptureContext(AdmissionOffering offering) => new
	{
		offering.Id, offering.TenantId, offering.AdmissionCycleId, offering.CampusId, offering.BoardId,
		offering.MediumId, offering.StageId, offering.StandardId, offering.Track, offering.EntryMode,
		offering.WorkflowDefinitionId, offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId,
		offering.IsActive, offering.OpenDate, offering.CloseDate
	};
}
