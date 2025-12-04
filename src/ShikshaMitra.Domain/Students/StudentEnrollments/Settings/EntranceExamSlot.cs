using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string? SlotName { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public int StudentLimit { get; private set; }
	public int RoomNo { get; private set; }

	protected EntranceExamSlot() { } // For ORM

	internal EntranceExamSlot(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string? slotName,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		AcademicYearId = academicYearId;
		SlotName = slotName;
		StartDate = startDate;
		EndDate = endDate;
		StudentLimit = studentLimit;
		RoomNo = roomNo;
	}

	internal void Update(
		string? slotName,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
	{
		SlotName = slotName;
		StartDate = startDate;
		EndDate = endDate;
		StudentLimit = studentLimit;
		RoomNo = roomNo;
	}

	/// <summary>
	/// Gets the duration of the exam slot in minutes
	/// </summary>
	public int GetDurationInMinutes()
	{
		return (int)(EndDate - StartDate).TotalMinutes;
	}

	/// <summary>
	/// Gets the date of the exam slot (without time component)
	/// </summary>
	public DateTime GetExamDate()
	{
		return StartDate.Date;
	}

	/// <summary>
	/// Gets the start time (time component only)
	/// </summary>
	public TimeSpan GetStartTime()
	{
		return StartDate.TimeOfDay;
	}

	/// <summary>
	/// Gets the end time (time component only)
	/// </summary>
	public TimeSpan GetEndTime()
	{
		return EndDate.TimeOfDay;
	}
}
