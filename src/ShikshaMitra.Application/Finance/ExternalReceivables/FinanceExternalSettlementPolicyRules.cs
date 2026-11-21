using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalSettlementPolicyRules(
	IRepository<FinancePolicyVersion, Guid> versions,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<CollectionPolicy> ReadCollectionAsync(FinanceExternalSettlementContext context, Guid versionId)
	{
		if (context.Link.CollectionPolicyVersionId.HasValue && context.Link.CollectionPolicyVersionId != versionId)
			throw Invalid();
		var version = await ReadVersionAsync(context, versionId, "collection");
		try
		{
			var snapshot = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
			if (snapshot.Status != CollectionPolicyStatus.Active) throw Invalid();
			return CollectionPolicy.FromSnapshot(version.PolicyId, version.TenantId, version.AcademicYearId,
				version.VersionNumber, snapshot);
		}
		catch (JsonException) { throw Invalid(); }
	}

	public async Task ValidateRefundReferenceAsync(FinanceExternalSettlementContext context, Guid? versionId)
	{
		await ReadRefundReferenceAsync(context, versionId);
	}

	public async Task ValidateRefundCreationAsync(FinanceExternalSettlementContext context, Guid? versionId)
	{
		var version = await ReadRefundReferenceAsync(context, versionId);
		if (version != null) EnsureRefundCreationCompatible(version, context.Source.Payer.Kind);
	}

	public static void EnsureRefundCreationCompatible(FinancePolicyVersion version, FinanceExternalPayerKind payerKind)
	{
		RefundPolicySnapshot? snapshot;
		try { snapshot = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson); }
		catch (JsonException) { throw Invalid(); }
		if (snapshot == null || snapshot.PolicyId != version.PolicyId || snapshot.AcademicYearId != version.AcademicYearId ||
			snapshot.Status != RefundPolicyStatus.Active ||
			payerKind == FinanceExternalPayerKind.Employee && snapshot.MaximumPerStudentYear.HasValue)
			throw Invalid();
	}

	private async Task<FinancePolicyVersion?> ReadRefundReferenceAsync(FinanceExternalSettlementContext context, Guid? versionId)
	{
		if (context.Link.RefundPolicyVersionId.HasValue && context.Link.RefundPolicyVersionId != versionId)
			throw Invalid();
		return versionId.HasValue ? await ReadVersionAsync(context, versionId.Value, "refund") : null;
	}

	private async Task<FinancePolicyVersion> ReadVersionAsync(FinanceExternalSettlementContext context, Guid id, string type)
	{
		var version = id == Guid.Empty ? null : await versions.FindAsync(id, includeDetails: false);
		if (version == null || version.TenantId != tenant.Id || version.TenantId != context.Link.TenantId ||
			version.AcademicYearId != context.Link.AcademicYearId || version.PolicyType != type ||
			!string.Equals(version.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw Invalid();
		return version;
	}

	private static BusinessException Invalid() => new(LibraryFinanceErrorCodes.InvalidFinancePolicy,
		"Choose a compatible recorded Finance policy version for this source.");
}
