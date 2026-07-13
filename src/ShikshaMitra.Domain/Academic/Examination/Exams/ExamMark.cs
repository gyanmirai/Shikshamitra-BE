using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamMark : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid DivisionId { get; private set; }
    public double? MarksObtained { get; private set; }
    public bool IsAbsent { get; private set; }
    public ExamAttendanceStatus AttendanceStatus { get; private set; }
    public string? Feedback { get; private set; }
    public bool IsPublished { get; private set; }

    private ExamMark() { }

    public ExamMark(
        Guid id,
        Guid examId,
        Guid studentId,
        Guid subjectId,
        Guid divisionId,
        double? marksObtained,
        bool isAbsent,
        string? feedback = null,
        Guid? tenantId = null) : base(id)
    {
        ExamId = examId;
        StudentId = studentId;
        SubjectId = subjectId;
        DivisionId = divisionId;
        MarksObtained = marksObtained;
        IsAbsent = isAbsent;
        Feedback = feedback;
        TenantId = tenantId;
    }

    public void Update(double? marksObtained, bool isAbsent, string? feedback)
    {
        MarksObtained = marksObtained;
        IsAbsent = isAbsent;
        Feedback = feedback;
    }

    public void UpdateAttendance(ExamAttendanceStatus attendanceStatus)
    {
        AttendanceStatus = attendanceStatus;
        IsAbsent = attendanceStatus != ExamAttendanceStatus.Present;
        if (attendanceStatus != ExamAttendanceStatus.Present)
        {
            MarksObtained = null;
            Feedback = null;
        }
    }

    public void Publish()
    {
        IsPublished = true;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }
}
