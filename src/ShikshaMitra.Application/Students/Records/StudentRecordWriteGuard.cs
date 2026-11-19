using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

/// <summary>Records edits enter the audited command path; admission section writes retain their lifecycle.</summary>
public class StudentRecordWriteGuard(StudentRecordApplyContext applying, StudentRecordsAccessService access) : ITransientDependency
{
	public bool IsApplying(Guid studentId, string operation) => applying.Allows(studentId, Section(operation));
	public async Task EnsureAsync(Guid studentId, string operation)
	{
		var section = Section(operation);
		if (applying.Allows(studentId, section)) return;
		await access.EnsureWritableAsync(studentId, section == "placement");
		throw new BusinessException("Students:Records:UseChangeRequest")
			.WithData("StudentId", studentId).WithData("Section", section);
	}
	private static string Section(string operation) => operation switch
	{
		"UpdateGeneralInfoAsync" => "general", "UpdateFamilyInfoAsync" => "family",
		"UpdateCommunicationInfoAsync" => "communication", "UpdateAdditionalDetailsAsync" => "additional",
		"UpdateMedicalRecordsAsync" => "medical", "UpdateBankDetailsAsync" => "bank",
		"UpdatePreviousSchoolDetailsAsync" => "previous-school",
		"ChangeStudentStatusAsync" => "status", "UploadPhotoAsync" => "photo", _ => throw new BusinessException("Students:Records:InvalidSection")
	};
}
