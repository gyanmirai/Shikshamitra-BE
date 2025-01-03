using ShikshaMitra.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

using ShikshaMitra.Students.studentenrollment.Inquiry;

public class StudentInquiryManager(IStudentInquiryRepository studentInquiryRepository, IGuidGenerator guidGenerator) : DomainService
{
	public async Task<StudentInquiry> CreateStudentInquiryAsync(
		string firstName,
		string middleName,
		string lastName,
		string formNumber,
		string previousSchoolName,
		DateTimeOffset dateOfBirth,
		DateTimeOffset dateOfInquiry,
		GenderType genderType,
		string parentContactNumber,
		string alternativeContactNumber,
		string city,
		string state,
		int pinCode,
		string address,
		Guid educationMediumId,
		Guid educationBoardId,
		Guid academicYearId,
		Guid standerdId,
		Guid inquiryPreferenceId
		)
	{
		string inquiryNumber = await GenerateInquiryNumberAsync();

		return new StudentInquiry(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			inquiryNumber: inquiryNumber,
			firstName: firstName,
			middleName: middleName,
			lastName: lastName,
			formNumber: formNumber,
			previousSchoolName: previousSchoolName,
			dateOfBirth: dateOfBirth,
			dateOfInquiry: dateOfInquiry,
			genderType: genderType,
			parentContactNumber: parentContactNumber,
			alternativeContactNumber: alternativeContactNumber,
			city: city,
			state: state,
			pinCode: pinCode,
			address: address,
			educationMediumId: educationMediumId,
			educationBoardId: educationBoardId,
			academicYearId: academicYearId,
			standerdId: standerdId,
			inquiryPreferenceId: inquiryPreferenceId
		);
	}
	
	private async Task<string> GenerateInquiryNumberAsync()
	{
		// Get the last Student Inquiry
		StudentInquiry? lastInquiry = await studentInquiryRepository.GetLastStudentInquiryAsync();

		int nextNumber = 1;

		if (lastInquiry != null)
		{
			var lastCode = lastInquiry.InquiryNumber;

			// Extract the numeric part from the last code (e.g., "INQ001" -> 1)
			if (int.TryParse(lastCode[3..], out int lastNumber))
			{
				nextNumber = lastNumber + 1;
			}
		}

		// Format the new InquiryNumber as "INQ" followed by a 3-digit number with leading zeros
		var newInquiryNumber = $"INQ{nextNumber:D3}";

		return newInquiryNumber;
	}
}