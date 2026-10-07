using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquiryCaptureAppService : IApplicationService
{
	Task<AdmissionInquiryCaptureContextDto> GetContextAsync(Guid offeringId);
	Task<AdmissionInquiryCaptureCatalogueDto> GetCatalogueAsync(Guid? academicYearId = null);
}
