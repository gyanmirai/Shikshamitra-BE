using System;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionInquiryAcademicSelectionValidator
{
	public static void EnsureMatches(CreateAdmissionInquiryDto input, AdmissionOffering offering)
	{
		if (input.BoardId == Guid.Empty || input.MediumId == Guid.Empty || input.StandardId == Guid.Empty)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Board, Medium, and Class are required to create an inquiry.");
		if (offering.BoardId != input.BoardId || offering.MediumId != input.MediumId ||
			offering.StandardId != input.StandardId)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"The selected Admission Offering does not match the selected Board, Medium, and Class.");
		if (input.AdmissionCycleId != Guid.Empty && offering.AdmissionCycleId != input.AdmissionCycleId)
			throw new BusinessException(AdmissionErrorCodes.InquiryCycleNotFound,
				"The selected Admission Offering does not belong to the supplied Admission Cycle.");
	}
}
