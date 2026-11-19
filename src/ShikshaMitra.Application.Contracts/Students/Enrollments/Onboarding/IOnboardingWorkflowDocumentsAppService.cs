using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public interface IOnboardingWorkflowDocumentsAppService : IApplicationService
{
	Task<List<AdmissionDocumentVerificationDto>> GetAsync(Guid id);
	Task<AdmissionDocumentVerificationDto> UploadAsync(Guid id, Guid stageId, string code, AdmissionDocumentUploadInput input);
	Task<AdmissionDocumentVerificationDto> RecordOriginalAsync(Guid id, Guid stageId, string code, AdmissionDocumentActionInput input);
	Task<AdmissionDocumentVerificationDto> ReviewAsync(Guid id, Guid evidenceId, AdmissionDocumentReviewInput input);
}
