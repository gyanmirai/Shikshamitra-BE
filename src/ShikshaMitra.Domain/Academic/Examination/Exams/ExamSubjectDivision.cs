using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamSubjectDivision : Entity<Guid>
{
    public Guid ExamSubjectId { get; private set; }
    public Guid DivisionId { get; private set; }
    public int Capacity { get; private set; }
    public List<Guid> RoomIds { get; private set; }

    private ExamSubjectDivision()
    {
        RoomIds = new List<Guid>();
    }

    public ExamSubjectDivision(Guid id, Guid examSubjectId, Guid divisionId, int capacity, List<Guid> roomIds) : base(id)
    {
        ExamSubjectId = examSubjectId;
        DivisionId = divisionId;
        Capacity = capacity;
        RoomIds = roomIds ?? new List<Guid>();
    }

    internal void SetExamSubjectId(Guid examSubjectId)
    {
        ExamSubjectId = examSubjectId;
    }

    public void UpdateRooms(List<Guid> roomIds, int capacity)
    {
        RoomIds = roomIds ?? new List<Guid>();
        Capacity = capacity;
    }
}
