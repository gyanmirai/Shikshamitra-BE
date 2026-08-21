using System; using System.Linq; using Volo.Abp; using Volo.Abp.Domain.Entities.Auditing; using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionGuardian : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid FamilyId { get; private set; } public string FirstName { get; private set; } public string? LastName { get; private set; } public string Mobile { get; private set; } public string? Email { get; private set; } public AdmissionGuardianRelationship Relationship { get; private set; } public bool IsPrimaryContact { get; private set; }
	protected AdmissionGuardian() { }
	public AdmissionGuardian(Guid id, Guid? tenantId, Guid familyId, string firstName, string? lastName, string mobile, string? email, AdmissionGuardianRelationship relationship, bool isPrimaryContact) : base(id) { TenantId = tenantId; FamilyId = Check.NotDefaultOrNull<Guid>(familyId, nameof(familyId)); FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim(); LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(); Mobile = NormalizePhone(mobile); Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(); Relationship = relationship; IsPrimaryContact = isPrimaryContact; }
	public void UpdateContact(string firstName, string? lastName, string mobile, string? email)
	{
		FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim(); LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(); Mobile = NormalizePhone(mobile); Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
	}
	private static string NormalizePhone(string phone) => string.Concat(Check.NotNullOrWhiteSpace(phone, nameof(phone)).Where(char.IsDigit));
}
