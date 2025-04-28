using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Students.StudentRecords.BulkUpload;

/// <summary>
/// DTO representing a student record from a CSV file
/// </summary>
public class CsvStudentDto
{
    // Basic Properties
    public string? FormNumber { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; } 
    public string? Email { get; set; }
    public string? ParentContactNumber { get; set; }
    public string? AlternativeContactNumber { get; set; }
    
    // Address Details
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? CountryName { get; set; } 
	public string? CountryIsoCode { get; set; } // Will be converted to Country object
	public string? StateName { get; set; }
	public string? StateIsoCode { get; set; }
    public string? City { get; set; }
    public string? PinCode { get; set; }
    
    // Personal Details
    public string? AadhaarNo { get; set; }
    public string? Religion { get; set; }
    public string? FamilyCast { get; set; }
    public string? CastType { get; set; } // Will be converted to CastType enum
    public string? MotherTongue { get; set; }
    public string? Nationality { get; set; }
    public string? BirthPlace { get; set; }
    public string? Remarks { get; set; }
    
    // Academic Details
    public string? GrNoRecord { get; set; } // Will be parsed to int?
    public string? TempGrNo { get; set; } // Will be parsed to int?
    public string? RollNo { get; set; } // Will be parsed to int?
    public string? AcademicYearId { get; set; } // Will be parsed to Guid
    public string? EducationBoardId { get; set; } // Will be parsed to Guid
    public string? EducationMediumId { get; set; } // Will be parsed to Guid
    public string? EducationMediumStageId { get; set; } // Will be parsed to Guid?
    public string? StandardId { get; set; } // Will be parsed to Guid?
    public string? DivisionId { get; set; } // Will be parsed to Guid?
    public string? Status { get; set; } // Will be converted to StudentStatus enum
    
    // Father Details
    public string? FatherName { get; set; }
    public string? FatherDOB { get; set; } // Format: yyyy-MM-dd
    public string? FatherOccupation { get; set; }
    public string? FatherQualification { get; set; }
    public string? FatherContactNo { get; set; }
    public string? FatherEmail { get; set; }
    public string? FatherAadhaarNo { get; set; }
    
    // Mother Details
    public string? MotherName { get; set; }
    public string? MotherDOB { get; set; } // Format: yyyy-MM-dd
    public string? MotherOccupation { get; set; }
    public string? MotherQualification { get; set; }
    public string? MotherContactNo { get; set; }
    public string? MotherEmail { get; set; }
    public string? MotherAadhaarNo { get; set; }
    
    // Guardian Details
    public string? GuardianName { get; set; }
    public string? GuardianDOB { get; set; } // Format: yyyy-MM-dd
    public string? GuardianOccupation { get; set; }
    public string? GuardianQualification { get; set; }
    public string? GuardianContactNo { get; set; }
    public string? GuardianEmail { get; set; }
    public string? GuardianAadhaarNo { get; set; }
    
    // Previous School Details
    public string? PreviousSchoolName { get; set; }
    public string? PreviousSchoolAddress { get; set; }
    public string? PreviousSchoolBoard { get; set; }
    public string? PreviousSchoolMedium { get; set; }
    public string? TcNo { get; set; }
    public string? LastClassPassed { get; set; }
    public string? Percentage { get; set; } // Will be parsed to float?
    
    // Bank Details
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    
    // Medical Records
    public string? Weight { get; set; } // Will be parsed to float?
    public string? Height { get; set; } // Will be parsed to float?
    public string? BodyMassIndex { get; set; } // Will be parsed to float?
    public string? Haemoglobin { get; set; } // Will be parsed to float?
    public string? Allergies { get; set; }
    public string? CovidVaccination { get; set; } // Will be converted to CovidVaccineDoses enum
    public string? PulseRate { get; set; } // Will be parsed to float?
    public string? ChildImmunisation { get; set; }
    public string? ImmunisationRemarks { get; set; }
}