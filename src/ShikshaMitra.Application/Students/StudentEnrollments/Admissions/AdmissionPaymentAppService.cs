using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionPayments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionPaymentAppService(AdmissionPaymentReadService reader, AdmissionPaymentCommandService commands)
	: ApplicationService, IAdmissionPaymentAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionPaymentStageDto> GetAsync(Guid applicationId, Guid stageId) => reader.GetAsync(applicationId, stageId);
	[Authorize(Permissions.Record)]
	public Task<AdmissionPaymentStageDto> RecordAsync(Guid applicationId, Guid stageId, RecordAdmissionPaymentDto input) =>
		commands.RecordAsync(applicationId, stageId, input);
	[Authorize(Permissions.Verify)]
	public Task<AdmissionPaymentStageDto> ReviewAsync(Guid transactionId, ReviewAdmissionPaymentDto input) =>
		commands.ReviewAsync(transactionId, input);
	[Authorize(Permissions.Waive)]
	public Task<AdmissionPaymentStageDto> WaiveAsync(Guid applicationId, Guid stageId, AdmissionPaymentReasonDto input) =>
		commands.WaiveAsync(applicationId, stageId, input);
	[Authorize(Permissions.Reverse)]
	public Task<AdmissionPaymentStageDto> ReverseAsync(Guid transactionId, AdmissionPaymentReasonDto input) =>
		commands.ReverseAsync(transactionId, input);
}
