using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPaymentAppService : IApplicationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.View)]
	Task<AdmissionPaymentStageDto> GetAsync(Guid applicationId, Guid stageId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Record)]
	Task<AdmissionPaymentStageDto> RecordAsync(Guid applicationId, Guid stageId, RecordAdmissionPaymentDto input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Verify)]
	Task<AdmissionPaymentStageDto> ReviewAsync(Guid transactionId, ReviewAdmissionPaymentDto input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Waive)]
	Task<AdmissionPaymentStageDto> WaiveAsync(Guid applicationId, Guid stageId, AdmissionPaymentReasonDto input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Reverse)]
	Task<AdmissionPaymentStageDto> ReverseAsync(Guid transactionId, AdmissionPaymentReasonDto input);
}
