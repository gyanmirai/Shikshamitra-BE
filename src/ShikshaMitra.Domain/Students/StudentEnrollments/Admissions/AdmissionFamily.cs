using System; using Volo.Abp; using Volo.Abp.Domain.Entities.Auditing; using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionFamily : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public string FamilyNumber { get; private set; } public AdmissionFamilyStatus Status { get; private set; } public Guid? PrimaryGuardianId { get; private set; }
	protected AdmissionFamily() { }
	public AdmissionFamily(Guid id, Guid? tenantId, string familyNumber) : base(id) { TenantId = tenantId; FamilyNumber = Check.NotNullOrWhiteSpace(familyNumber, nameof(familyNumber)); Status = AdmissionFamilyStatus.Active; }
	public void SetPrimaryGuardian(Guid guardianId) => PrimaryGuardianId = Check.NotDefaultOrNull<Guid>(guardianId, nameof(guardianId));
}
