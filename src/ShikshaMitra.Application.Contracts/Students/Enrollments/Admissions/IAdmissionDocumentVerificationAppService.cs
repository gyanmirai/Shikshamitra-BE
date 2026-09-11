using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionDocumentVerificationAppService : IApplicationService
{
	Task<List<AdmissionDocumentTypeDto>> GetDocumentTypesAsync();
	Task<AdmissionDocumentVerificationDto> GetAsync(Guid applicationId, Guid stageInstanceId);
	Task<AdmissionDocumentVerificationDto> UploadAsync(Guid applicationId, Guid stageInstanceId, string requirementCode, AdmissionDocumentUploadInput input);
	Task<AdmissionDocumentVerificationDto> RecordOriginalAsync(Guid applicationId, Guid stageInstanceId, string requirementCode, AdmissionDocumentActionInput input);
	Task<AdmissionDocumentVerificationDto> WaiveAsync(Guid applicationId, Guid stageInstanceId, string requirementCode, AdmissionDocumentWaiverInput input);
	Task<AdmissionDocumentVerificationDto> ReviewAsync(Guid evidenceId, AdmissionDocumentReviewInput input);
	Task<AdmissionDocumentDownloadDto> DownloadAsync(Guid evidenceId);
}
