using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Setup;

public sealed class HrSetupConfigurationRevision : Entity, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid Revision { get; private set; }
	public override object[] GetKeys() => [TenantId!];
}

public sealed class HrSetupReadinessSnapshot : Entity, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid Revision { get; private set; }
	public bool IsReady { get; private set; }
	public DateTime CheckedAtUtc { get; private set; }
	public DateTime ValidUntilUtc { get; private set; }
	public override object[] GetKeys() => [TenantId!, AcademicYearId];
}

public interface IHrSetupReadinessStore
{
	Task<Guid> ReadRevisionAsync();
	Task<bool?> ReadStatusAsync(Guid academicYearId, DateTime nowUtc);
	Task RecordAsync(Guid academicYearId, Guid revision, bool isReady, DateTime checkedAtUtc, DateTime validUntilUtc);
}
