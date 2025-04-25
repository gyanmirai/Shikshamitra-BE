
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Repositories;
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
        Guid primaryTeacherId,
        List<Guid> secondaryTeacherIds,
        DateTimeOffset scheduledDate,
        string subjectColor,
        string? learningGoals,
        string? prerequisites,
        string? description)
    {
        Subject subject = new Subject(
            guidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            academicBoardId,
            academicMediumId,
            classId,
            subjectCode,
            subjectName,
            primaryTeacherId,
            scheduledDate,
            subjectColor,
            learningGoals,
            prerequisites,
            description,
            uploadedDocument: null
        );

        // call method to add secondary teachers in a loop  
        foreach (var teacherId in secondaryTeacherIds)
        {
            subject.AddSecondaryTeacher(guidGenerator.Create(), teacherId);
        }

		return subject;
    }
}
