using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;

namespace ShikshaMitra.Students.StudentRecords.BulkUpload;

public class StudentCsvImportService(
	IStudentRepository studentRepository,
	ILogger<StudentCsvImportService> logger,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IEducationMediumStageRepository educationMediumStageRepository,
	IStandardRepository standardRepository,
	IDivisionRepository divisionRepository) : IStudentCsvImportService, ITransientDependency
{
	public async Task<StudentCsvUploadResultDto> ImportStudentsFromCsvAsync(Stream stream)
	{
		var result = new StudentCsvUploadResultDto();
		var rowNumber = 0;

		try
		{
			using var reader = new StreamReader(stream);
			using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
				MissingFieldFound = null
			});

			var records = csv.GetRecords<CsvStudentDto>().ToList();
			var studentsToAdd = new List<Student>();

			foreach (var record in records)
			{
				rowNumber++;
				result.TotalCount++;

				try
				{
					var student = await ConvertCsvRecordToStudentAsync(record);
					if (student != null)
					{
						studentsToAdd.Add(student);
						result.SuccessCount++;
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error processing CSV record at row {RowNumber}", rowNumber);
					result.FailureCount++;
					result.ValidationErrors.Add(new StudentCsvValidationError
					{
						RowNumber = rowNumber,
						Identifier = GetRecordIdentifier(record),
						ErrorMessage = ex.Message
					});
				}
			}

			// Bulk insert all valid students
			if (studentsToAdd.Any())
			{
				await studentRepository.InsertManyAsync(studentsToAdd);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error processing CSV file");
			throw new UserFriendlyException("Failed to process the CSV file. Please check the file format.");
		}

		return result;
	}
	private async Task<Student?> ConvertCsvRecordToStudentAsync(CsvStudentDto record)
	{
		// Basic validation
		if (string.IsNullOrWhiteSpace(record.FirstName) || string.IsNullOrWhiteSpace(record.LastName))
		{
			throw new UserFriendlyException("First name and last name are required");
		}

		// Parse enums and other types
		GenderType? gender = null;
		if (!string.IsNullOrWhiteSpace(record.Gender))
		{
			if (!Enum.TryParse<GenderType>(record.Gender, true, out var parsedGender))
			{
				throw new UserFriendlyException($"Invalid gender: {record.Gender}. Expected values: Male, Female, Other");
			}
			gender = parsedGender;
		}

		Country? country = null;
		if (!string.IsNullOrWhiteSpace(record.CountryName) && !string.IsNullOrEmpty(record.CountryIsoCode))
		{
			{
				country = new Country(record.CountryName, record.CountryIsoCode);
			}

			State? state = null;
			if (!string.IsNullOrWhiteSpace(record.StateName) && !string.IsNullOrWhiteSpace(record.StateIsoCode))
			{
				{
					state = new State(record.StateName, record.StateIsoCode);
				}

				CastType? castType = null;
				if (!string.IsNullOrWhiteSpace(record.CastType))
				{
					if (!Enum.TryParse<CastType>(record.CastType, true, out var parsedCastType))
					{
						throw new UserFriendlyException($"Invalid cast type: {record.CastType}");
					}
					castType = parsedCastType;
				}
				DateTimeOffset? dob = null;
				if (!string.IsNullOrWhiteSpace(record.DateOfBirth))
				{
					if (!DateTimeOffset.TryParseExact(record.DateOfBirth, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDob))
					{
						throw new UserFriendlyException($"Invalid date of birth: {record.DateOfBirth}. Expected format: dd-MM-yyyy");
					}
					dob = parsedDob;
				}                   // Parse the academic details
				var grNoRecord = ParseInt(record.GrNoRecord, "GR number");
				var tempGrNo = ParseInt(record.TempGrNo, "Temporary GR number");
				var rollNo = ParseInt(record.RollNo, "Roll number");

				// Find academic entities by name instead of requiring GUIDs directly
				var academicYearId = await GetAcademicYearIdAsync(record.AcademicYearId);
				var educationBoardId = await GetEducationBoardIdAsync(record.EducationBoardId, academicYearId);
				var educationMediumId = await GetEducationMediumIdAsync(record.EducationMediumId, educationBoardId);
				var educationMediumStageId = await GetEducationMediumStageIdAsync(record.EducationMediumStageId, educationMediumId);
				var standardId = await GetStandardIdAsync(record.StandardId, educationMediumStageId);

				Guid? divisionId = await GetDivisionIdAsync(record.DivisionId, standardId); 
		

				// Parse status
				var status = StudentStatus.Active;
				if (!string.IsNullOrWhiteSpace(record.Status))
				{
					if (!Enum.TryParse<StudentStatus>(record.Status, true, out var parsedStatus))
					{
						throw new UserFriendlyException($"Invalid status: {record.Status}. Expected values: Active, Left, Inactive");
					}
					status = parsedStatus;
				}

				// Create father details
				var fatherDetails = CreateContactDetails(
					record.FatherName,
					record.FatherDOB,
					record.FatherOccupation,
					record.FatherQualification,
					record.FatherContactNo,
					record.FatherEmail,
					record.FatherAadhaarNo
				);

				// Create mother details
				var motherDetails = CreateContactDetails(
					record.MotherName,
					record.MotherDOB,
					record.MotherOccupation,
					record.MotherQualification,
					record.MotherContactNo,
					record.MotherEmail,
					record.MotherAadhaarNo
				);

				// Create guardian details
				var guardianDetails = CreateContactDetails(
					record.GuardianName,
					record.GuardianDOB,
					record.GuardianOccupation,
					record.GuardianQualification,
					record.GuardianContactNo,
					record.GuardianEmail,
					record.GuardianAadhaarNo
				);

				// Create previous school details
				var previousSchoolDetails = new PreviousSchoolDetails(
					record.PreviousSchoolName,
					record.PreviousSchoolAddress,
					record.PreviousSchoolBoard,
					record.PreviousSchoolMedium,
					record.TcNo,
					record.LastClassPassed,
					ParseFloat(record.Percentage, "Percentage")
				);

				// Create parent bank details
				var parentBankDetails = new ParentBankDetails(
					record.BankName,
					record.BranchName,
					record.AccountHolderName,
					record.AccountNumber,
					record.IfscCode
				);

				// Create student communication details
				var studentCommunicationDetails = new StudentCommunicationDetails(
					primaryContactNo: record.ParentContactNumber,
					parentContactNo: record.FatherContactNo ?? record.ParentContactNumber,
					parentEmail: record.FatherEmail ?? record.Email,
					studentContactNo: record.AlternativeContactNumber,
					studentEmail: record.Email,
					whatsAppNo: record.AlternativeContactNumber ?? record.ParentContactNumber,
					localAddressLine1: record.AddressLine1,
					localAddressLine2: record.AddressLine2,
					localAddressLine3: record.AddressLine3,
					localCountryIsoCode: record.CountryIsoCode,
					localCountryName: record.CountryName,
					localStateIsoCode: record.StateIsoCode,
					localStateName: record.StateName,
					localCity: record.City,
					localPinCode: record.PinCode,
					permanentAddressLine1: record.AddressLine1,
					permanentAddressLine2: record.AddressLine2,
					permanentAddressLine3: record.AddressLine3,
					permanentCountryIsoCode: record.CountryIsoCode,
					permanentCountryName: record.CountryName,
					permanentStateIsoCode: record.StateIsoCode,
					permanentStateName: record.StateName,
					permanentCity: record.City,
					permanentPinCode: record.PinCode,
					emergencyAddressLine1: record.AddressLine1,
					emergencyAddressLine2: record.AddressLine2,
					emergencyAddressLine3: record.AddressLine3,
					emergencyCountry: record.CountryName,
					emergencyState: record.StateName,
					emergencyCity: record.City,
					emergencyPinCode: record.PinCode,
					remarks: record.Remarks
				);

				// Parse category from string to enum
				Category? category = null;
				if (!string.IsNullOrWhiteSpace(record.Category))
				{
					if (!Enum.TryParse<Category>(record.Category, true, out var parsedCategory))
					{
						throw new UserFriendlyException($"Invalid category: {record.Category}");
					}
					category = parsedCategory;
				}

				// Parse boolean fields
				var rightToEducation = ParseBool(record.RightToEducation, "Right to education");
				var bPLStudent = ParseBool(record.BPLStudent, "BPL student");
				var personWithDisability = ParseBool(record.PersonWithDisability, "Person with disability");
				var singleParentChild = ParseBool(record.SingleParentChild, "Single parent child");
				var singleChild = ParseBool(record.SingleChild, "Single child");

				// Create student additional details
				var studentAdditionalDetails = new StudentAdditionalDetails(
					category: category,
					rightToEducation: rightToEducation,
					bPLStudent: bPLStudent,
					bPLCardNo: record.BPLCardNo,
					personWithDisability: personWithDisability,
					secondLanguage: record.SecondLanguage,
					emergencyContactNo: record.EmergencyContactNo ?? record.GuardianContactNo ?? record.MotherContactNo,
					singleParentChild: singleParentChild,
					singleChild: singleChild,
					sponsorName: record.SponsorName
				);

				// Create medical records
				var studentMedicalRecords = new StudentMedicalRecords(
					weight: ParseFloat(record.Weight, "Weight"),
					height: ParseFloat(record.Height, "Height"),
					bodyMassIndex: ParseFloat(record.BodyMassIndex, "Body Mass Index"),
					haemoglobin: ParseFloat(record.Haemoglobin, "Haemoglobin"),
					allergies: record.Allergies,
					covidVaccination: ParseCovidVaccinationStatus(record.CovidVaccination),
					pulseRate: ParseFloat(record.PulseRate, "Pulse Rate"),
					childImmunisation: record.ChildImmunisation,
					immunisationRemarks: record.ImmunisationRemarks
				);                  // Create the student entity
				return new Student(
					guidGenerator.Create(),
					currentTenant.Id,
					record.FormNumber,
					record.FirstName,
					record.MiddleName,
					record.LastName,
					dob,
					gender,
					record.Email,
					record.ParentContactNumber,
					record.AlternativeContactNumber,
					Array.Empty<byte>(), // No profile picture from CSV

					// Address
					record.AddressLine1,
					record.AddressLine2,
					record.AddressLine3,
					country,
					state,
					record.City,
					record.PinCode,

					// Personal Details
					record.AadhaarNo,
					record.Religion,
					record.FamilyCast,
					castType,
					record.MotherTongue,
					record.Nationality,
					record.BirthPlace,
					record.Remarks,
					null, // No user ID from CSV

					// Academic Details
					academicYearId,
					educationBoardId,
					educationMediumId,
					educationMediumStageId,
					standardId,
					divisionId,
					tempGrNo,
					grNoRecord,
					rollNo,

					// Family Details
					fatherDetails,
					motherDetails,
					guardianDetails,

					// Additional Details
					studentCommunicationDetails,
					studentAdditionalDetails,
					studentMedicalRecords,
					previousSchoolDetails,
					parentBankDetails,
					new List<StudentDocument>() // Empty document list
				);
			}
		}

		return null;
	}

	private string GetRecordIdentifier(CsvStudentDto record)
	{
		if (!string.IsNullOrWhiteSpace(record.FormNumber))
			return $"Form #{record.FormNumber}";

		var name = new[] { record.FirstName, record.MiddleName, record.LastName }
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Select(n => n?.Trim())
			.ToArray();

		return string.Join(" ", name);
	}

	private StudentContactDetails CreateContactDetails(
		string? name,
		string? dob,
		string? occupation,
		string? qualification,
		string? contactNo,
		string? email,
		string? aadhaarNo)
	{
		DateTimeOffset? parsedDob = null;
		if (!string.IsNullOrWhiteSpace(dob) && DateTimeOffset.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dobValue))
		{
			parsedDob = dobValue;
		}

		return new StudentContactDetails(
			name,
			parsedDob,
			occupation,
			qualification,
			contactNo,
			email,
			null, // passOutYear
			null, // officeNo
			null, // officeAddress
			null, // designation
			null, // organisation
			aadhaarNo,
			null, // remarks
			null  // address
		);
	}

	private int? ParseInt(string? value, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (int.TryParse(value, out var result))
			return result;

		throw new UserFriendlyException($"Invalid {fieldName} value: {value}. Expected a number.");
	}
	private float? ParseFloat(string? value, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			return result;

		throw new UserFriendlyException($"Invalid {fieldName} value: {value}. Expected a number.");
	}

	private bool? ParseBool(string? value, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = value.Trim().ToLowerInvariant();

		return value switch
		{
			"true" or "yes" or "1" or "y" => true,
			"false" or "no" or "0" or "n" => false,
			_ => throw new UserFriendlyException($"Invalid {fieldName} value: {value}. Expected a boolean value like 'true'/'false', 'yes'/'no', etc.")
		};
	}

	private Guid ParseGuid(string? value, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new UserFriendlyException($"{fieldName} is required.");

		if (Guid.TryParse(value, out var result))
			return result;

		throw new UserFriendlyException($"Invalid {fieldName}: {value}. Expected a valid GUID.");
	}

	private CovidVaccineDoses? ParseCovidVaccinationStatus(string? value)
	{
		return value switch
		{
			null or { Length: 0 } => null,

			_ when Enum.TryParse<CovidVaccineDoses>(value, true, out var result) => result,

			"0" or "none" => CovidVaccineDoses.None,
			"1" or "one" => CovidVaccineDoses.FirstDose,
			"2" or "two" => CovidVaccineDoses.SecondDose,
			"3" or "three" => CovidVaccineDoses.ThirdDose,
			"4" or "four" => CovidVaccineDoses.FourthDose,

			_ => throw new UserFriendlyException($"Invalid Covid vaccination status: {value}. Expected values: None, One, Two, ThreeOrMore, or numeric values 0-3")
		};
	}

	// Helper methods to find entities by name or ID
	private async Task<Guid> GetAcademicYearIdAsync(string? nameOrId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Academic year is required.");
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await academicYearRepository.AnyAsync(ay => ay.Id == parsedId);
			if (exists)
			{
				return parsedId;
			}
			throw new UserFriendlyException($"Academic year with ID {nameOrId} not found.");
		}

		// Look up by name
		var academicYear = await academicYearRepository.FirstOrDefaultAsync(ay => ay.Name == nameOrId); if (academicYear is null)
		{
			throw new UserFriendlyException($"Academic year '{nameOrId}' not found.");
		}

		return academicYear.Id;
	}

	private async Task<Guid> GetEducationBoardIdAsync(string? nameOrId, Guid academicYearId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Education board is required.");
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await educationBoardRepository.AnyAsync(eb => eb.Id == parsedId);
			if (exists)
			{
				return parsedId;
			}
			throw new UserFriendlyException($"Education board with ID {nameOrId} not found.");
		}

		if (!Enum.TryParse<EducationBoardType>(nameOrId, true, out var educationBoardType))
		{
			throw new UserFriendlyException($"Invalid education board type: {nameOrId}. Expected values: {string.Join(", ", Enum.GetNames(typeof(EducationBoardType)))}");
		}

		// Look up by name
		var educationBoard = await educationBoardRepository.FirstOrDefaultAsync(eb => eb.Type == educationBoardType && eb.AcademicYearId == academicYearId);
		return educationBoard is null ? throw new UserFriendlyException($"Education board '{nameOrId}' not found.") : educationBoard.Id;
	}

	private async Task<Guid> GetEducationMediumIdAsync(string? nameOrId, Guid educationBoardId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Education medium is required.");
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await educationMediumRepository.AnyAsync(em => em.Id == parsedId && em.EducationBoardId == educationBoardId);
			if (exists)
			{
				return parsedId;
			}
			throw new UserFriendlyException($"Education medium with ID {nameOrId} not found.");
		}

		// Look up by name
		var educationMedium = await educationMediumRepository.FirstOrDefaultAsync(em => em.Name == nameOrId); if (educationMedium is null)
		{
			throw new UserFriendlyException($"Education medium '{nameOrId}' not found.");
		}

		return educationMedium.Id;
	}

	private async Task<Guid> GetEducationMediumStageIdAsync(string? nameOrId, Guid educationMediumId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Education medium stage is required.");
		}
		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await educationMediumStageRepository.AnyAsync(ems => ems.Id == parsedId && ems.EducationMediumId == educationMediumId);
			if (exists)
			{
				return parsedId;
			}
			throw new UserFriendlyException($"Education medium stage with ID {nameOrId} not found.");
		}
		// Look up by name
		var educationMediumStage = await educationMediumStageRepository.FirstOrDefaultAsync(ems => ems.Name == nameOrId); if (educationMediumStage is null)
		{
			throw new UserFriendlyException($"Education medium stage '{nameOrId}' not found.");
		}
		return educationMediumStage.Id;
	}

	private async Task<Guid> GetStandardIdAsync(string? nameOrId, Guid educationMediumStageId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Standard is required.");
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await standardRepository.AnyAsync(s => s.Id == parsedId);
			if (exists)
			{
				return parsedId;
			}
			throw new UserFriendlyException($"Standard with ID {nameOrId} not found.");
		}

		// Look up by name
		var standard = await standardRepository.FirstOrDefaultAsync(s => s.Name == nameOrId && s.EducationMediumStageId == educationMediumStageId);
		return standard is null ? throw new UserFriendlyException($"Standard '{nameOrId}' not found.") : standard.Id;
	}

	private async Task<Guid> GetDivisionIdAsync(string? nameOrId, Guid standardId)
	{
		if (string.IsNullOrWhiteSpace(nameOrId))
		{
			throw new UserFriendlyException("Division is required.");
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(nameOrId, out var parsedId))
		{
			var exists = await divisionRepository.AnyAsync(d => d.Id == parsedId);
			return exists ? parsedId : throw new UserFriendlyException($"Division with ID {nameOrId} not found.");
		}

		// Look up by name
		var division = await divisionRepository.FirstOrDefaultAsync(d => d.Name == nameOrId && d.StandardId == standardId);
		return division is null ? throw new UserFriendlyException($"Division '{nameOrId}' not found.") : division.Id;
	}
}