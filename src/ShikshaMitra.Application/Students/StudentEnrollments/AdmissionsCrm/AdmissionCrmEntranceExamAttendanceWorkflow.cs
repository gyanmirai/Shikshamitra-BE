using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamAttendanceWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	AdmissionCrmEntranceExamAccess access,
	AdmissionCrmEntranceExamTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmEntranceExamDto> MarkAsync(
		Guid examRecordId,
		MarkAdmissionCrmExamAttendanceDto input)
	{
		var exam = await access.GetExamAsync(examRecordId, "MarkAttendanceAsync");
		var lead = await access.GetLeadAsync(exam.LeadId, "MarkAttendanceAsync");
		exam.MarkAttendance(input.AttendanceStatus, Clock.Now);
		AdmissionCrmEntranceExamMapper.ApplyLeadStage(lead, exam);
		await examRepository.UpdateAsync(exam, autoSave: false);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ExamAttendanceMarked,
			"Exam attendance marked", input.AttendanceStatus.ToString(), exam.Id);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		return AdmissionCrmEntranceExamMapper.ToDto(exam, lead);
	}
}
