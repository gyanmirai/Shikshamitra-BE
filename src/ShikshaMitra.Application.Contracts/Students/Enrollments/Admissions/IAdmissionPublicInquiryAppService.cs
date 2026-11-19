using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPublicInquiryAppService
{
	Task<List<AdmissionPublicInquiryLinkDto>> GetLinksAsync();
	Task<List<AdmissionPublicInquiryLinkOptionDto>> GetLinkOptionsAsync(Guid admissionCycleId);
	Task<AdmissionPublicInquiryLinkCreatedDto> CreateLinkAsync(CreateAdmissionPublicInquiryLinkDto input);
	Task<AdmissionPublicInquiryLinkDto> SetLinkFormAsync(Guid id, SetAdmissionPublicInquiryLinkFormDto input);
	Task RevokeLinkAsync(Guid id, string concurrencyStamp);
	Task<PublicAdmissionInquiryContextDto> GetPublicAsync(string token, Guid? offeringId = null);
	Task<ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetDownloadDto> GetPublicBrandingAssetAsync(string token,
		ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetKind kind, Guid? offeringId = null);
	Task SubmitPublicAsync(string token, SubmitPublicAdmissionInquiryDto input);
}
