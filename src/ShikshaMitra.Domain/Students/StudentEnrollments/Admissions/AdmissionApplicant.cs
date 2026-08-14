using System; using Volo.Abp; using Volo.Abp.Domain.Entities.Auditing; using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionApplicant : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid FamilyId { get; private set; } public string FirstName { get; private set; } public string? MiddleName { get; private set; } public string? LastName { get; private set; } public DateTime? DateOfBirth { get; private set; } public string? Gender { get; private set; } public Guid? ExistingStudentId { get; private set; } public AdmissionApplicantStatus Status { get; private set; }
	protected AdmissionApplicant() { }
	public AdmissionApplicant(Guid id, Guid? tenantId, Guid familyId, string firstName, string? middleName, string? lastName, DateTime? dateOfBirth, string? gender, Guid? existingStudentId) : base(id) { TenantId = tenantId; FamilyId = Check.NotDefaultOrNull<Guid>(familyId, nameof(familyId)); FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim(); MiddleName = Normalize(middleName); LastName = Normalize(lastName); DateOfBirth = dateOfBirth?.Date; Gender = Normalize(gender); ExistingStudentId = existingStudentId; Status = AdmissionApplicantStatus.Active; }
	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
