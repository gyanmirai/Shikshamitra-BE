using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamBulkWorkflow(
	AdmissionCrmEntranceExamAttendanceWorkflow attendanceWorkflow,
	AdmissionCrmEntranceExamResultWorkflow resultWorkflow) : ITransientDependency
{
	public Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(
		Guid examRecordId,
		MarkAdmissionCrmExamAttendanceDto input) => attendanceWorkflow.MarkAsync(examRecordId, input);

	public Task<AdmissionCrmEntranceExamDto> UploadResultAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input,
		string? correctionReason) => resultWorkflow.UploadFromBulkAsync(examRecordId, input, correctionReason);
}
