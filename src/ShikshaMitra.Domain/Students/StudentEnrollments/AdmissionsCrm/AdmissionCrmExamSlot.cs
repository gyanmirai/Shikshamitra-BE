using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmExamSlot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? PlaybookId { get; private set; }
	public string? SlotTemplateCode { get; private set; }
	public string? ExamDefinitionCode { get; private set; }
	public string SlotName { get; private set; } = string.Empty;
	public string? Campus { get; private set; }
	public string? RoomOrHall { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public int StudentLimit { get; private set; }
	public int RoomNo { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmExamSlot()
	{
	}

	public AdmissionCrmExamSlot(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? playbookId,
		string? slotTemplateCode,
		string? examDefinitionCode,
		string slotName,
		string? campus,
		string? roomOrHall,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		PlaybookId = playbookId;
		SlotTemplateCode = NormalizeOptional(slotTemplateCode, AdmissionCrmConsts.MaxPlaybookCodeLength);
		ExamDefinitionCode = NormalizeOptional(examDefinitionCode, AdmissionCrmConsts.MaxPlaybookCodeLength);
		Update(slotName, campus, roomOrHall, startDate, endDate, studentLimit, roomNo);
		IsActive = true;
	}

	public void Update(
		string slotName,
		string? campus,
		string? roomOrHall,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
	{
		if (startDate >= endDate) throw new BusinessException("AdmissionCrm:InvalidExamSlotWindow");
		if (studentLimit <= 0) throw new BusinessException("AdmissionCrm:InvalidExamSlotCapacity");
		if (roomNo <= 0) throw new BusinessException("AdmissionCrm:InvalidExamSlotRoom");
		SlotName = Check.NotNullOrWhiteSpace(slotName, nameof(slotName), AdmissionCrmConsts.MaxNameLength);
		Campus = NormalizeOptional(campus, AdmissionCrmConsts.MaxNameLength);
		RoomOrHall = NormalizeOptional(roomOrHall, AdmissionCrmConsts.MaxNameLength);
		StartDate = startDate;
		EndDate = endDate;
		StudentLimit = studentLimit;
		RoomNo = roomNo;
	}

	public void SetActive(bool active) => IsActive = active;

	public void EnsureCanAcceptReservation(DateTimeOffset now)
	{
		if (!IsActive)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotInactive")
				.WithData("EntranceExamSlotId", Id);
		}

		if (StartDate <= now)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotAlreadyStarted")
				.WithData("EntranceExamSlotId", Id);
		}
	}

	private static string? NormalizeOptional(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
}
