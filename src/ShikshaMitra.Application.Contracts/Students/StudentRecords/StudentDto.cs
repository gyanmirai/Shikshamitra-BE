using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentDto : AuditedEntityDto<Guid>
{
	#region Basic Properties
	public string FormNumber { get; set; }
	public string FirstName { get; set; }
	public string MiddleName { get; set; }
	public string LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public GenderType? GenderType { get; set; }
	public string Email { get; set; }
	public string ParentContactNumber { get; set; }
	public string AlternativeContactNumber { get; set; }
	public byte[] ProfilePicture { get; set; }
	#endregion

	#region Group Information
	public StudentGroupMembershipInfo GroupMembership { get; set; }
	#endregion

	#region Address Details
	public string AddressLine1 { get; set; }
	public string AddressLine2 { get; set; }
	public string AddressLine3 { get; set; }
	public Country? Country { get; set; }
	public State? State { get; set; }
	public string City { get; set; }
	public string PinCode { get; set; }
	#endregion

	#region Personal Details
	public string AadhaarNo { get; set; }
	public string Religion { get; set; }
	public string FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public string MotherTongue { get; set; }
	public string Nationality { get; set; }
	public string BirthPlace { get; set; }
	public string Remarks { get; set; }
	public Guid? UserId { get; set; }
	#endregion

	#region Academic Details
	public int? GrNoRecord { get; set; }
	public int? TempGrNo { get; set; }
	public int? RollNo { get; set; }

	public StudentStatus Status { get; set; }


	public Guid AcademicYearId { get; set; }
	public AcademicYearDto AcademicYear { get; set; }

	public Guid EducationBoardId { get; set; }
	public EducationBoardDto EducationBoard { get; set; }

	public Guid EducationMediumId { get; set; }
	public EducationMediumDto EducationMedium { get; set; }

	public Guid? EducationMediumStageId { get; set; }
	public EducationMediumStageDto EducationMediumStage { get; set; }

	public Guid? StandardId { get; set; }
	public StandardDto Standard { get; set; }

	public Guid? DivisionId { get; set; }
	public DivisionDto Division { get; set; }
	#endregion

	#region Family Details
	public StudentContactDetailsDto FatherDetails { get; set; }
	public StudentContactDetailsDto MotherDetails { get; set; }
	public StudentContactDetailsDto GuardianDetails { get; set; }
	#endregion

	#region Additional Details
	public StudentCommunicationDetailsDto StudentCommunicationDetails { get; set; }
	public StudentAdditionalDetailsDto StudentAdditionalDetails { get; set; }
	public StudentMedicalRecordsDto StudentMedicalRecords { get; set; }
	public PreviousSchoolDetailsDto PreviousSchoolDetails { get; set; }
	public ParentBankDetailsDto ParentBankDetails { get; set; }
	#endregion
}

public class StudentGroupMembershipInfo
{
	public Guid? GroupId { get; set; }
	public string GroupName { get; set; }
	public bool IsLeader { get; set; }  // Main leader
	public bool IsDivisionLeader { get; set; }  // Division leader
}