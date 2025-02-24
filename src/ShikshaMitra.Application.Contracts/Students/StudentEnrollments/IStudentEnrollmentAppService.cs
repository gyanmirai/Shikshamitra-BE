using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments;

public interface IStudentEnrollmentAppService : ICrudAppService<StudentEnrollmentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentEnrollmentDto>
{
	// Task<StudentEnrollmentDto> GetStudentEnrollmentByUserId(Guid userId);

	Task<StudentEnrollmentDto> CreateStudentInquiryAsync(CreateStudentInquiryDto input);
	Task<StudentEnrollmentDto> UpdateStudentBasicDetails(Guid studentEnrollmentId, CreateUpdateStudentRegistrationDto input);
	Task<StudentEnrollmentDto> UpdateFamilyDetails(Guid studentEnrollmentId, UpdateStudentFamilyDetailsDto input);
	Task<StudentEnrollmentDto> UpdateParentBankDetails(Guid studentEnrollmentId, ParentBankDetailsDto input);
	Task<StudentEnrollmentDto> UpdatePreviousSchoolDetails(Guid studentEnrollmentId, PreviousSchoolDetailsDto input);
	Task<StudentEnrollmentDto> UpdateStudentAdditionalDetails(Guid studentEnrollmentId, StudentAdditionalDetailsDto input);
	Task<StudentEnrollmentDto> UpdateStudentCommunicationDetails(Guid studentEnrollmentId, StudentCommunicationDetailsDto input);
	Task<StudentEnrollmentDto> UpdateStudentMedicalRecords(Guid studentEnrollmentId, StudentMedicalRecordsDto input);


	// For the documents
	Task UploadDocumentsAsync(Guid studentEnrollmentId, List<DocumentDto> documentDtos);
	Task<DocumentDto> GetDocumentAsync(Guid studentEnrollmentId, Guid documentId);
	Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid studentEnrollmentId);
	Task<DocumentDto> UpdateDocumentAsync(Guid studentEnrollmentId, Guid documentId, DocumentDto documentDto);

	Task<PagedResultDto<StudentEnrollmentDto>> GetStudentsFollowUp(Guid academicYearId);
	Task<CustomFileResult> GenerateReportAsync(StudentEnrollmentReportType reportType);
}