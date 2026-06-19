using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

// ─── Marks Workflow: Draft / Submit / Verify / Approve / Lock ─────────────────
//
//  POST /api/academics/exams/{examId}/marks/draft
//  POST /api/academics/exams/{examId}/marks/submit
//  POST /api/academics/exams/{examId}/marks/verify
//  POST /api/academics/exams/{examId}/marks/approve
//  POST /api/academics/exams/{examId}/marks/lock
//
//  All five workflow-transition endpoints share the same query parameters and
//  response shape.  The route path differentiates the action.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Query parameters shared by all marks-workflow transition endpoints
/// (draft / submit / verify / approve / lock).
/// </summary>
public class MarksWorkflowActionInput
{
    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }
}

/// <summary>
/// Response returned by every marks-workflow transition endpoint.
/// </summary>
public class MarksWorkflowActionResponseDto
{
    /// <summary>Human-readable confirmation message.</summary>
    public string Message { get; set; } = string.Empty;

    public Guid ExamId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>
    /// The new workflow status after the transition.
    /// 0 = Draft | 1 = Submitted | 2 = Verified | 3 = Approved | 4 = Published | 5 = Locked
    /// </summary>
    public int WorkflowStatus { get; set; }

    public DateTime UpdatedAt { get; set; }
}

// ─── PATCH /api/academics/exams/{examId}/students/{studentId}/attendance ───────
//
//  Query param : subjectId (Guid)
//  Request body: UpdateStudentAttendanceDto
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request body for updating a student's exam attendance status.
/// </summary>
public class UpdateStudentAttendanceDto
{
    /// <summary>
    /// 0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted
    /// </summary>
    [Required]
    [Range(0, 3)]
    public int AttendanceStatus { get; set; }
}

/// <summary>
/// Response returned after updating a student's exam attendance status.
/// </summary>
public class UpdateStudentAttendanceResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid ExamId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>
    /// 0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted
    /// </summary>
    public int AttendanceStatus { get; set; }
}

// ─── GET /api/academics/exams/marks/summary ───────────────────────────────────
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Query parameters for the exam marks summary endpoint.
/// </summary>
public class GetExamMarksSummaryInput
{
    [Required]
    public Guid ExamId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    public Guid? StandardId { get; set; }
}

/// <summary>
/// Summary information for a single exam/subject/division combination,
/// returned by GET /api/academics/exams/marks/summary.
/// Mirrors the <c>IExamSummary</c> type on the frontend.
/// </summary>
public class GetExamMarksSummaryResponseDto
{
    public Guid ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string DivisionName { get; set; } = string.Empty;

    public double MaxMarks { get; set; }
    public double PassingMarks { get; set; }
    public DateTime? ExamDate { get; set; }

    public int TotalStudents { get; set; }
    public int MarksEntered { get; set; }
    public int Pending { get; set; }

    /// <summary>
    /// 0 = Draft | 1 = Submitted | 2 = Verified | 3 = Approved | 4 = Published | 5 = Locked
    /// </summary>
    public int WorkflowStatus { get; set; }

    public int PendingPublishedCount { get; set; }
}
