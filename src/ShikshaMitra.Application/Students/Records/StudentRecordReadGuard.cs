using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

/// <summary>Shared registration reads honor the record policy once a student is operational.</summary>
public class StudentRecordReadGuard(StudentRecordsAccessService access, IPermissionChecker permissions) : ITransientDependency
{
	public async Task EnsureAsync(Guid studentId, string section)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var record = await access.EnsureReadableAsync(studentId);
		if (record.Access != "Full") throw new BusinessException("Students:Records:SummaryOnly");
		if (StudentRecordVisibility.IsMasked(record.Policy, section, await access.GetDatabaseRoleNamesAsync()))
			throw new BusinessException("Students:Records:RestrictedFields");
	}
	public static string Section(string operation) => operation switch
	{
		"GetRegistrationGeneralDetailsByIdAsync" => "general", "GetFamilyInfoAsync" => "family",
		"GetCommunicationInfoAsync" => "communication", "GetAdditionalDetailsAsync" => "additional",
		"GetMedicalRecordsAsync" => "medical", "GetBankDetailsAsync" => "bank",
		"GetPreviousSchoolDetailsAsync" => "previous-school", "GetPhotoAsync" => "photo",
		_ => throw new BusinessException("Students:Records:InvalidSection")
	};
}
