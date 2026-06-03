using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ClassroomObservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid ObserverEmployeeId { get; private set; }
	public DateTime ObservationDate { get; private set; }
	public string? ClassName { get; private set; }
	public string? SubjectName { get; private set; }
	public int LessonObjectiveScore { get; private set; }
	public int StudentEngagementScore { get; private set; }
	public int ClassroomManagementScore { get; private set; }
	public int AssessmentScore { get; private set; }
	public string? Notes { get; private set; }

	protected ClassroomObservation()
	{
	}

	public ClassroomObservation(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid observerEmployeeId, DateTime observationDate, string? className, string? subjectName, int lessonObjectiveScore, int studentEngagementScore, int classroomManagementScore, int assessmentScore, string? notes) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		ObserverEmployeeId = observerEmployeeId;
		ObservationDate = observationDate.Date;
		ClassName = string.IsNullOrWhiteSpace(className) ? null : className.Trim();
		SubjectName = string.IsNullOrWhiteSpace(subjectName) ? null : subjectName.Trim();
		LessonObjectiveScore = Math.Clamp(lessonObjectiveScore, 1, 5);
		StudentEngagementScore = Math.Clamp(studentEngagementScore, 1, 5);
		ClassroomManagementScore = Math.Clamp(classroomManagementScore, 1, 5);
		AssessmentScore = Math.Clamp(assessmentScore, 1, 5);
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}
}
