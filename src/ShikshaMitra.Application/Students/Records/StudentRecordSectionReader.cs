using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

/// <summary>Internal Details reads still execute each canonical owner's normal guard and mapper.</summary>
[RemoteService(false)]
public class StudentRecordSectionReader(StudentGeneralInfoAppService general, StudentFamilyInfoAppService family,
	StudentCommunicationInfoAppService communication, StudentMedicalRecordsAppService medical,
	StudentBankDetailsAppService bank, StudentPreviousSchoolDetailsAppService previous) : ITransientDependency
{
	internal async Task<StudentRecordSectionReadResult?> ReadAsync(Guid studentId, string section, StudentRecordSectionReadScope scope)
	{
		if (section == "general")
		{
			var result = await general.GetRecordsSectionAsync(studentId, scope);
			return new(result.Details, true, result.DivisionId, result.ProfilePictureUrl);
		}
		return section switch
		{
			"family" => new(await family.GetFamilyInfoAsync(studentId, scope)),
			"communication" => new(await communication.GetCommunicationInfoAsync(studentId, scope)),
			"medical" => new(await medical.GetMedicalRecordsAsync(studentId, scope)),
			"bank" => new(await bank.GetBankDetailsAsync(studentId, scope)),
			"previous-school" => new(await previous.GetPreviousSchoolDetailsAsync(studentId, scope)),
			_ => null
		};
	}
}

internal sealed record StudentRecordSectionReadResult(object Value, bool HasGeneralFields = false, Guid? DivisionId = null, string? ProfilePictureUrl = null);
internal sealed record StudentRecordGeneralSection(StudentGeneralDetailsDto Details, Guid? DivisionId, string? ProfilePictureUrl);
