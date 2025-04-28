using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords.BulkUpload
{
	public class StudentCsvImportService : IStudentCsvImportService, ITransientDependency
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILogger<StudentCsvImportService> _logger;
        private readonly IGuidGenerator _guidGenerator;
        private readonly ICurrentTenant _currentTenant;

        public StudentCsvImportService(
            IStudentRepository studentRepository,
            ILogger<StudentCsvImportService> logger,
            IGuidGenerator guidGenerator,
            ICurrentTenant currentTenant)
        {
            _studentRepository = studentRepository;
            _logger = logger;
            _guidGenerator = guidGenerator;
            _currentTenant = currentTenant;
        }

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
                        var student = ConvertCsvRecordToStudent(record);
                        if (student != null)
                        {
                            studentsToAdd.Add(student);
                            result.SuccessCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing CSV record at row {RowNumber}", rowNumber);
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
                    await _studentRepository.InsertManyAsync(studentsToAdd);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file");
                throw new UserFriendlyException("Failed to process the CSV file. Please check the file format.");
            }

            return result;
        }

		private Student? ConvertCsvRecordToStudent(CsvStudentDto record)
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
						if (!DateTimeOffset.TryParse(record.DateOfBirth, out var parsedDob))
						{
							throw new UserFriendlyException($"Invalid date of birth: {record.DateOfBirth}. Expected format: yyyy-MM-dd");
						}
						dob = parsedDob;
					}

					// Parse the academic details
					int? grNoRecord = ParseInt(record.GrNoRecord, "GR number");
					int? tempGrNo = ParseInt(record.TempGrNo, "Temporary GR number");
					int? rollNo = ParseInt(record.RollNo, "Roll number");

					Guid academicYearId = ParseGuid(record.AcademicYearId, "Academic year ID");
					Guid educationBoardId = ParseGuid(record.EducationBoardId, "Education board ID");
					Guid educationMediumId = ParseGuid(record.EducationMediumId, "Education medium ID");

					Guid? educationMediumStageId = null;
					if (!string.IsNullOrWhiteSpace(record.EducationMediumStageId))
					{
						educationMediumStageId = ParseGuid(record.EducationMediumStageId, "Education medium stage ID");
					}

					Guid standardId = ParseGuid(record.StandardId, "Standard ID");

					Guid? divisionId = null;
					if (!string.IsNullOrWhiteSpace(record.DivisionId))
					{
						divisionId = ParseGuid(record.DivisionId, "Division ID");
					}

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

					// Create student additional details
					var studentAdditionalDetails = new StudentAdditionalDetails(
						category: null, // Not available in CSV
						rightToEducation: record.FamilyCast?.ToLower().Contains("rte") ?? false,
						bPLStudent: record.FamilyCast?.ToLower().Contains("bpl") ?? false,
						bPLCardNo: null, // Not available in CSV
						personWithDisability: null, // Not available in CSV
						secondLanguage: null, // Not available in CSV
						emergencyContactNo: record.GuardianContactNo ?? record.MotherContactNo,
						singleParentChild: null, // Not available in CSV
						singleChild: null, // Not available in CSV
						sponsorName: null  // Not available in CSV
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
					);

					// Create the student entity
					return new Student(
						_guidGenerator.Create(),
						_currentTenant.Id,
						record.FormNumber,
						record.FirstName,
						record.MiddleName,
						record.LastName,
						dob,
						gender,
						record.Email,
						record.ParentContactNumber,
						record.AlternativeContactNumber,
						null, // No profile picture from CSV

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
						null // No documents from CSV
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
            if (!string.IsNullOrWhiteSpace(dob) && DateTimeOffset.TryParse(dob, out var dobValue))
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
	}
}