using System; using Volo.Abp; using Volo.Abp.Domain.Entities.Auditing; using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionApplicant : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid FamilyId { get; private set; } public string FirstName { get; private set; } public string? MiddleName { get; private set; } public string? LastName { get; private set; } public DateTime? DateOfBirth { get; private set; } public string? Gender { get; private set; } public Guid? ExistingStudentId { get; private set; } public string? AddressLine1 { get; private set; } public string? AddressLine2 { get; private set; } public string? City { get; private set; } public string? State { get; private set; } public string? Country { get; private set; } public string? PostalCode { get; private set; } public AdmissionApplicantStatus Status { get; private set; }
	protected AdmissionApplicant() { }
	/// <summary>Immutable identity for a future new Student; this is not a Student foreign key.</summary>
	public Guid ReservedStudentId { get; private set; }

	public AdmissionApplicant(Guid id, Guid? tenantId, Guid familyId, string firstName, string? middleName, string? lastName, DateTime? dateOfBirth, string? gender, Guid? existingStudentId, Guid reservedStudentId, string? addressLine1 = null, string? addressLine2 = null, string? city = null, string? state = null, string? country = null, string? postalCode = null) : base(id) { TenantId = tenantId; FamilyId = Check.NotDefaultOrNull<Guid>(familyId, nameof(familyId)); ReservedStudentId = Check.NotDefaultOrNull<Guid>(reservedStudentId, nameof(reservedStudentId)); FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim(); MiddleName = Normalize(middleName); LastName = Normalize(lastName); DateOfBirth = dateOfBirth?.Date; Gender = Normalize(gender); ExistingStudentId = existingStudentId; AddressLine1 = Normalize(addressLine1); AddressLine2 = Normalize(addressLine2); City = Normalize(city); State = Normalize(state); Country = Normalize(country); PostalCode = Normalize(postalCode); Status = AdmissionApplicantStatus.Active; }
	public void UpdateProfile(string firstName, string? middleName, string? lastName, DateTime? dateOfBirth, string? gender, string? addressLine1 = null, string? addressLine2 = null, string? city = null, string? state = null, string? country = null, string? postalCode = null)
	{
		FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim(); MiddleName = Normalize(middleName); LastName = Normalize(lastName); DateOfBirth = dateOfBirth?.Date; Gender = Normalize(gender); AddressLine1 = Normalize(addressLine1); AddressLine2 = Normalize(addressLine2); City = Normalize(city); State = Normalize(state); Country = Normalize(country); PostalCode = Normalize(postalCode);
	}
	public void LinkStudent(Guid studentId)
	{
		if (studentId == Guid.Empty) throw new ArgumentException("Student ID is required.", nameof(studentId));
		if (ExistingStudentId.HasValue && ExistingStudentId.Value != studentId)
			throw new BusinessException(ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingErrorCodes.ExistingStudentInvalid);
		ExistingStudentId = studentId;
	}
	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
