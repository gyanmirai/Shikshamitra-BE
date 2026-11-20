using System;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalReceivableSourceLink : BasicAggregateRoot<Guid>, IMultiTenant, ICreationAuditedObject
{
	public DateTime CreationTime { get; private set; }
	public Guid? CreatorId { get; private set; }
	public string LegacyConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");
	public string LegacyExtraPropertiesJson { get; private set; } = "{}";
	public Guid? TenantId { get; private set; }
	public FinanceExternalSourceKind SourceKind { get; private set; }
	public Guid SourceId { get; private set; }
	public string SourceVersion { get; private set; } = string.Empty;
	public string SourceFingerprint { get; private set; } = string.Empty;
	public string SourceSnapshotJson { get; private set; } = string.Empty;
	public Guid FinanceChargeRequestId { get; private set; }
	public Guid FinanceChargeId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Currency { get; private set; } = string.Empty;
	public Guid? CollectionPolicyVersionId { get; private set; }
	public Guid? RefundPolicyVersionId { get; private set; }

	private FinanceExternalReceivableSourceLink() { }

	public FinanceExternalReceivableSourceLink(Guid id, Guid tenantId, FinanceExternalSourceKind sourceKind,
		Guid sourceId, string sourceVersion, string sourceFingerprint, string sourceSnapshotJson,
		Guid requestId, Guid chargeId, Guid yearId, string currency,
		Guid? collectionPolicyVersionId, Guid? refundPolicyVersionId) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || sourceKind != FinanceExternalSourceKind.LibraryAssessment ||
			sourceId == Guid.Empty || requestId == Guid.Empty || chargeId == Guid.Empty || yearId == Guid.Empty ||
			string.IsNullOrWhiteSpace(sourceVersion) || sourceVersion.Length > 64 ||
			string.IsNullOrWhiteSpace(sourceFingerprint) || sourceFingerprint.Length != 64 ||
			string.IsNullOrWhiteSpace(sourceSnapshotJson) || currency != "INR" ||
			collectionPolicyVersionId == Guid.Empty || refundPolicyVersionId == Guid.Empty)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		TenantId = tenantId;
		SourceKind = sourceKind;
		SourceId = sourceId;
		SourceVersion = sourceVersion;
		SourceFingerprint = sourceFingerprint;
		SourceSnapshotJson = sourceSnapshotJson;
		FinanceChargeRequestId = requestId;
		FinanceChargeId = chargeId;
		AcademicYearId = yearId;
		Currency = currency;
		CollectionPolicyVersionId = collectionPolicyVersionId;
		RefundPolicyVersionId = refundPolicyVersionId;
	}
}
