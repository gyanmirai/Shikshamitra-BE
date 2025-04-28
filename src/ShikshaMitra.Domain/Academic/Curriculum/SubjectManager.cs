
using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectManager(
	IGuidGenerator guidGenerator
   ) : DomainService
{
    public Subject CreateSubject(
        Guid academicYearId,
        Guid academicBoardId,
        Guid academicMediumId,
        Guid classId,
        string subjectCode,
        string subjectName,
        DateTimeOffset scheduledDate,
        string subjectColor,
        string? learningGoals,
        string? prerequisites,
        string? description,
		SubjectStatus status)
    {
        return new Subject(
            guidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            academicBoardId,
            academicMediumId,
            classId,
            subjectCode,
            subjectName,
            scheduledDate,
            subjectColor,
            learningGoals,
            prerequisites,
            description,
			status
		);
    }
}
