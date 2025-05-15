
using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class TempSubjectManager(
	IGuidGenerator guidGenerator
   ) : DomainService
{
    public TempSubject CreateSubject(
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
        return new TempSubject(
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
