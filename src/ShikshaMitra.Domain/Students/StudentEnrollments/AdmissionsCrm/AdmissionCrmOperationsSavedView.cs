using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsSavedView : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid OwnerUserId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; private set; }
	public string FiltersJson { get; private set; } = "{}";
	public bool IsShared { get; private set; }

	protected AdmissionCrmOperationsSavedView() { }

	public AdmissionCrmOperationsSavedView(Guid id, Guid? tenantId, Guid academicYearId, Guid ownerUserId,
		string name, AdmissionCrmOperationsQueueType queueType, string filtersJson, bool isShared) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; OwnerUserId = ownerUserId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		if (!Enum.IsDefined(queueType)) throw new BusinessException("AdmissionCrm:InvalidOperationsQueueType");
		QueueType = queueType; FiltersJson = ValidateFilters(filtersJson); IsShared = isShared;
	}

	private static string ValidateFilters(string value)
	{
		var json = Check.NotNullOrWhiteSpace(value, nameof(value), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		using var document = JsonDocument.Parse(json);
		if (document.RootElement.ValueKind != JsonValueKind.Object) throw new BusinessException("AdmissionCrm:InvalidOperationsViewFilters");
		return json;
	}
}
