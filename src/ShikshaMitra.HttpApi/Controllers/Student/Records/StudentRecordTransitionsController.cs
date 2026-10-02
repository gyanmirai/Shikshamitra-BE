using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController, Area("app"), RemoteService(Name = "Default")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View)]
[Route("api/students/records/transitions")]
public class StudentRecordTransitionsController(IStudentRecordTransitionAppService service) : AbpController
{
	[HttpGet] public async Task<Result<PagedResultDto<StudentRecordTransitionDto>>> GetListAsync([FromQuery] StudentRecordTransitionQueryDto input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("{id:guid}")] public async Task<Result<StudentRecordTransitionDto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));
	[HttpGet("options")] public async Task<Result<StudentRecordTransitionOptionsDto>> OptionsAsync([FromQuery] Guid? academicYearId, [FromQuery] Guid? studentId, [FromQuery] Guid? admissionOfferingId = null) => Result.Success(await service.GetOptionsAsync(academicYearId, studentId, admissionOfferingId));
	[HttpPost("preview"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordTransitionPreviewDto>> PreviewAsync(StudentRecordTransitionBatchInput input) => Result.Success(await service.PreviewAsync(input));
	[HttpPost("batches"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordTransitionBatchResultDto>> SubmitAsync(StudentRecordTransitionBatchInput input) => Result.Success(await service.SubmitBatchAsync(input));
	[HttpPost("{id:guid}/approve"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve)]
	public async Task<Result<StudentRecordTransitionDto>> ApproveAsync(Guid id, StudentRecordTransitionDecisionDto input) => Result.Success(await service.ApproveAsync(id, input));
	[HttpPost("{id:guid}/reject"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve)]
	public async Task<Result<StudentRecordTransitionDto>> RejectAsync(Guid id, StudentRecordTransitionDecisionDto input) => Result.Success(await service.RejectAsync(id, input));
	[HttpPost("{id:guid}/cancel"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordTransitionDto>> CancelAsync(Guid id, StudentRecordTransitionDecisionDto input) => Result.Success(await service.CancelAsync(id, input));
	[HttpPost("{id:guid}/apply"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply)]
	public async Task<Result<StudentRecordTransitionDto>> ApplyAsync(Guid id, StudentRecordTransitionDecisionDto input) => Result.Success(await service.ApplyAsync(id, input));
	[HttpPost("{id:guid}/correct"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordTransitionPreviewRowDto>> CorrectAsync(Guid id, StudentRecordTransitionCorrectionDto input) => Result.Success(await service.CorrectAsync(id, input));
	[HttpPost("returns"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordReturnDto>> ReturnAsync(StudentRecordReturnInput input) => Result.Success(await service.ReturnAsync(input));
	[HttpPost("returns/preview"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordTransitionPreviewRowDto>> PreviewReturnAsync(StudentRecordReturnInput input) => Result.Success(await service.PreviewReturnAsync(input));
	[HttpGet("renewals")] public async Task<Result<PagedResultDto<StudentRecordRenewalDto>>> RenewalsAsync([FromQuery] StudentRecordTransitionQueryDto input) => Result.Success(await service.GetRenewalsAsync(input));
	[HttpGet("renewals/{id:guid}")] public async Task<Result<StudentRecordRenewalDto>> RenewalAsync(Guid id) => Result.Success(await service.GetRenewalAsync(id));
	[HttpPost("renewals/{id:guid}/confirm"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<Result<StudentRecordRenewalDto>> ConfirmAsync(Guid id, StudentRecordRenewalDecisionDto input) => Result.Success(await service.ConfirmRenewalAsync(id, input));
	[HttpPost("renewals/{id:guid}/activate"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply)]
	public async Task<Result<StudentRecordRenewalDto>> ActivateAsync(Guid id, StudentRecordRenewalDecisionDto input) => Result.Success(await service.ActivateRenewalAsync(id, input));
	[HttpGet("renewals/{id:guid}/portal-options")]
	public async Task<Result<StudentRecordRenewalPortalOptionsDto>> PortalOptionsAsync(Guid id) => Result.Success(await service.GetRenewalPortalOptionsAsync(id));
	[HttpPost("renewals/{id:guid}/portal-accounts"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create),
		Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit), Authorize(IdentityPermissions.Users.Create)]
	public async Task<Result<StudentPortalAccountDto>> ProvisionPortalAsync(Guid id, ProvisionStudentPortalAccountDto input) => Result.Success(await service.ProvisionRenewalPortalAsync(id, input));
}
