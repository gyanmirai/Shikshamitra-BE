using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public string GrNo { get; private set; }
	public string Surname { get; private set; }
	public string Name { get; private set; }
	public string MiddleName { get; private set; }
	public DateOnly DateOfBirth { get; private set; }
	public string Gender { get; private set; }

	// Foreign keys
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }

	// Navigation properties
	public Standard Standard { get; private set; }
	public Division Division { get; private set; }

	// Value objects
	public Address Address { get; private set; }
	public AdmissionDetails AdmissionDetails { get; private set; }
	public CommunicationDetails CommunicationDetails { get; private set; }
	public StudentParentDetails FatherDetails { get; private set; }
	public StudentParentDetails MotherDetails { get; private set; }

	// Profile Picture
	public byte[]? ProfilePicture { get; private set; }

	// Constructor for creating a new student
	private Student() { } // For ORM

	internal Student(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		string grNo,
		string surname,
		string name,
		string middleName,
		DateOnly dateOfBirth,
		string gender,
		Guid standardId,
		Guid divisionId,
		Address address,
		// AdmissionDetails admissionDetails,
		// CommunicationDetails communicationDetails,
		StudentParentDetails fatherDetails,
		StudentParentDetails motherDetails,
		byte[]? profilePicture = null) : base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = userId;
		GrNo = Check.NotNullOrWhiteSpace(grNo, nameof(grNo));
		Surname = Check.NotNullOrWhiteSpace(surname, nameof(surname));
		Name = Check.NotNullOrWhiteSpace(name, nameof(name));
		MiddleName = middleName;
		DateOfBirth = dateOfBirth;
		Gender = Check.NotNullOrWhiteSpace(gender, nameof(gender));
		StandardId = standardId;
		DivisionId = divisionId;
		Address = Check.NotNull(address, nameof(address));
		// AdmissionDetails = Check.NotNull(admissionDetails, nameof(admissionDetails));
		// CommunicationDetails = Check.NotNull(communicationDetails, nameof(communicationDetails));
		FatherDetails = Check.NotNull(fatherDetails, nameof(fatherDetails));
		MotherDetails = Check.NotNull(motherDetails, nameof(motherDetails));
		ProfilePicture = profilePicture;
	}
}