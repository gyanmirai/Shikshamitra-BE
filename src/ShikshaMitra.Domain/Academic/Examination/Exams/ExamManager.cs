using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamManager(
    IRepository<Exam, Guid> examRepository,
    IGuidGenerator guidGenerator
) : DomainService
{
    public Exam Create(Guid examTypeId, string examName, Guid standardId, List<Guid> divisionIds, List<Guid> subjectIds)
    {
        var exam = new Exam(guidGenerator.Create(), examTypeId, examName, standardId, CurrentTenant.Id);

        foreach (var divisionId in divisionIds)
        {
            exam.AddDivision(guidGenerator.Create(), divisionId);
        }

        foreach (var subjectId in subjectIds)
        {
            exam.AddSubject(new ExamSubject(guidGenerator.Create(), exam.Id, subjectId));
        }

        return exam;
    }

    public void UpdateBasicDetails(Exam exam, string examName, Guid examTypeId, Guid standardId, List<Guid> divisionIds, List<Guid> subjectIds)
    {
        exam.UpdateExamDetails(examName, examTypeId, standardId);
        exam.UpdateDivisions(divisionIds);
        exam.UpdateSubjects(subjectIds);
    }

    public void SaveSchedule(Exam exam, List<(Guid subjectId, DateTime? examDate, DateTime? startTime, DateTime? endTime, double? totalMarks, double? passingMarks, string? notes, Guid? gradeScalingId)> schedules)
    {
        foreach (var schedule in schedules)
        {
            var subject = exam.ExamSubjects.ToList().FirstOrDefault(x => x.SubjectId == schedule.subjectId);
            if (subject != null)
            {
                subject.UpdateSchedule(schedule.examDate, schedule.startTime, schedule.endTime, schedule.totalMarks, schedule.passingMarks, schedule.notes, schedule.gradeScalingId);
            }
        }
    }

    public void AssignRooms(Exam exam, bool isRoomAssigned, List<(Guid subjectId, List<(Guid divisionId, List<Guid> roomIds, int capacity)> divisions)> assignments)
    {
        // Logic to update room assignments
        // NOTE: The requirement mentions 'isRoomAssigned' flag at the request level, check if we need to store it on Exam?
        // Checking Exam entity, I don't see IsRoomAssigned property.
        // Assuming strictly updating the ExamSubjectDivision entities.

        foreach (var assignment in assignments)
        {
            var subject = exam.ExamSubjects.FirstOrDefault(x => x.SubjectId == assignment.subjectId);
            if (subject == null) continue;

            // Clear existing assignments if needed or update them?
            // Strategy: Sync assignments. using a similar approach to UpdateSubjects/Divisions but nested.

            var incomingDivisionIds = assignment.divisions.Select(d => d.divisionId).ToList();

            // Remove assignments for divisions not in the payload?
            // Or assume payload contains all divisions for the subject?
            // The API is "Assign exam rooms", usually implies full state update for that exam's room assignments.
            // Let's assume we update per subject.

            foreach (var divAssignment in assignment.divisions)
            {
                var existingAssignment = subject.RoomAssignments.FirstOrDefault(x => x.DivisionId == divAssignment.divisionId);

                if (existingAssignment != null)
                {
                    existingAssignment.UpdateRooms(divAssignment.roomIds, divAssignment.capacity);
                }
                else
                {
                    subject.AddRoomAssignment(new ExamSubjectDivision(
                        guidGenerator.Create(),
                        subject.Id,
                        divAssignment.divisionId,
                        divAssignment.capacity,
                        divAssignment.roomIds
                    ));
                }
            }

            // Optional: Remove assignments that are not in the incoming list?
            // If the user sends a partial list, we might lose data.
            // Given "Assign exam rooms" (PUT), it usually means replace.
            // But doing safe updates for now. To be fully PUT-compliant for the sub-resource, we should probably remove missing ones.
            // Let's implement removal of missing divisions for this subject.

            var assignmentsToRemove = subject.RoomAssignments
                .Where(x => !incomingDivisionIds.Contains(x.DivisionId))
                .ToList();

            foreach (var toRemove in assignmentsToRemove)
            {
                subject.RemoveRoomAssignment(toRemove.Id);
            }
        }
    }
}
