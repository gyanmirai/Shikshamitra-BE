using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPublicInquiryAppService
{
	Task<List<AdmissionPublicInquiryLinkDto>> GetLinksAsync();
	Task<AdmissionPublicInquiryLinkCreatedDto> CreateLinkAsync(CreateAdmissionPublicInquiryLinkDto input);
	Task<AdmissionPublicInquiryLinkDto> SetLinkFormAsync(Guid id, SetAdmissionPublicInquiryLinkFormDto input);
	Task RevokeLinkAsync(Guid id, string concurrencyStamp);
	Task<PublicAdmissionInquiryContextDto> GetPublicAsync(string token);
	Task SubmitPublicAsync(string token, SubmitPublicAdmissionInquiryDto input);
}
