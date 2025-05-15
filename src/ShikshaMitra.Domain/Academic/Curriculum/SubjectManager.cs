
using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectManager(
	IGuidGenerator guidGenerator
   ) : DomainService
{
    public StandardSubjectAssignment CreateSubject(
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
		return new StandardSubjectAssignment(
	  guidGenerator.Create(),
	  CurrentTenant.Id,
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
