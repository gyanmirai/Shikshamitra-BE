using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Route("api/students/records/{studentId:guid}/information")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordInformationController(
    IStudentGeneralInfoAppService general,
    IStudentFamilyInfoAppService family,
    IStudentCommunicationInfoAppService communication,
    IStudentAdditionalDetailsAppService additional,
    IStudentMedicalRecordsAppService medical,
    IStudentPreviousSchoolDetailsAppService previous,
    IStudentBankDetailsAppService bank) : AbpController
{
    [HttpGet("general")]
    public async Task<Result<StudentGeneralDetailsDto>> GetGeneralAsync(Guid studentId) =>
        Result.Success(await general.GetRegistrationGeneralDetailsByIdAsync(studentId));

    [HttpPut("general")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentGeneralDetailsDto>> UpdateGeneralAsync(Guid studentId, [FromBody] UpdateGeneralInfoDto input) =>
        Result.Success(await general.UpdateGeneralInfoAsync(studentId, input));

    [HttpGet("family")]
    public async Task<Result<StudentFamilyInfoDto>> GetFamilyAsync(Guid studentId) =>
        Result.Success(await family.GetFamilyInfoAsync(studentId));

    [HttpPut("family")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentFamilyInfoDto>> UpdateFamilyAsync(Guid studentId, [FromBody] UpdateFamilyInfoDto input) =>
        Result.Success(await family.UpdateFamilyInfoAsync(studentId, input));

    [HttpGet("communication")]
    public async Task<Result<StudentCommunicationInfoDto>> GetCommunicationAsync(Guid studentId) =>
        Result.Success(await communication.GetCommunicationInfoAsync(studentId));

    [HttpPut("communication")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentCommunicationInfoDto>> UpdateCommunicationAsync(Guid studentId, [FromBody] UpdateCommunicationInfoDto input) =>
        Result.Success(await communication.UpdateCommunicationInfoAsync(studentId, input));

    [HttpGet("additional")]
    public async Task<Result<StudentAdditionalDetailsDto>> GetAdditionalAsync(Guid studentId) =>
        Result.Success(await additional.GetAdditionalDetailsAsync(studentId));

    [HttpPut("additional")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentAdditionalDetailsDto>> UpdateAdditionalAsync(Guid studentId, [FromBody] UpdateAdditionalDetailsDto input) =>
        Result.Success(await additional.UpdateAdditionalDetailsAsync(studentId, input));

    [HttpGet("medical")]
    public async Task<Result<StudentMedicalRecordsDto>> GetMedicalAsync(Guid studentId) =>
        Result.Success(await medical.GetMedicalRecordsAsync(studentId));

    [HttpPut("medical")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentMedicalRecordsDto>> UpdateMedicalAsync(Guid studentId, [FromBody] UpdateMedicalRecordsDto input) =>
        Result.Success(await medical.UpdateMedicalRecordsAsync(studentId, input));

    [HttpGet("previous-school")]
    public async Task<Result<StudentPreviousSchoolDetailsDto>> GetPreviousSchoolAsync(Guid studentId) =>
        Result.Success(await previous.GetPreviousSchoolDetailsAsync(studentId));

    [HttpPut("previous-school")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentPreviousSchoolDetailsDto>> UpdatePreviousSchoolAsync(Guid studentId, [FromBody] UpdatePreviousSchoolDetailsDto input) =>
        Result.Success(await previous.UpdatePreviousSchoolDetailsAsync(studentId, input));

    [HttpGet("bank")]
    public async Task<Result<StudentBankDetailsDto>> GetBankAsync(Guid studentId) =>
        Result.Success(await bank.GetBankDetailsAsync(studentId));

    [HttpPut("bank")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
    public async Task<Result<StudentBankDetailsDto>> UpdateBankAsync(Guid studentId, [FromBody] UpdateBankDetailsDto input) =>
        Result.Success(await bank.UpdateBankDetailsAsync(studentId, input));
}
