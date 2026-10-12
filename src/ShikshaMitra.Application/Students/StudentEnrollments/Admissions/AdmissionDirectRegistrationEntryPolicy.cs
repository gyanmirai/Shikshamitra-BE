using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionDirectRegistrationEntryPolicy
{
	public static void EnsureAllowed(AdmissionOffering offering)
	{
		if (offering.Track is not (AdmissionTrack.Rte or AdmissionTrack.Management or AdmissionTrack.Staff) ||
			offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed,
				"Choose an RTE, Management, or Staff Offering configured to start with Registration. General admission starts with Inquiry.");
	}
}
