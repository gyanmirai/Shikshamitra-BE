using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public class ExamSeatAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SeatingPlanId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RoomId { get; private set; }
    public int SeatNumber { get; private set; }
    public Guid? BenchId { get; private set; }
    public bool IsRoomAssign { get; private set; }

    private ExamSeatAllocation() { }

    public ExamSeatAllocation(
        Guid id,
        Guid? tenantId,
        Guid seatingPlanId,
        Guid examId,
        Guid studentId,
        Guid roomId,
        int seatNumber,
        Guid? benchId,
        bool isRoomAssign) : base(id)
    {
        TenantId = tenantId;
        SeatingPlanId = seatingPlanId;
        ExamId = examId;
        StudentId = studentId;
        RoomId = roomId;
        SeatNumber = seatNumber;
        BenchId = benchId;
        IsRoomAssign = isRoomAssign;
    }

    public void Update(Guid seatingPlanId, Guid roomId, int seatNumber, Guid? benchId, bool isRoomAssign)
    {
        SeatingPlanId = seatingPlanId;
        RoomId = roomId;
        SeatNumber = seatNumber;
        BenchId = benchId;
        IsRoomAssign = isRoomAssign;
    }
}
