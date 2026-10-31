using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotManager(
	IRepository<EntranceExamSlot, Guid> entranceExamSlotRepository,
	IRepository<AcademicYear, Guid> academicYearRepository) : DomainService
{
	/// <summary>
	/// Creates a new entrance exam slot with validation
	/// </summary>
	public async Task<EntranceExamSlot> CreateAsync(
		Guid academicYearId,
		string? slotName,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
	{
		// Validate academic year exists
		await ValidateAcademicYearExistsAsync(academicYearId);

		// Validate business rules
		if (startDate >= endDate)
		{
			throw new BusinessException("SM:EntranceExamSlot:001")
				.WithData("startDate", startDate)
				.WithData("endDate", endDate);
		}

		if (studentLimit <= 0)
		{
			throw new BusinessException("SM:EntranceExamSlot:002")
				.WithData("studentLimit", studentLimit);
		}

		if (roomNo <= 0)
		{
			throw new BusinessException("SM:EntranceExamSlot:003")
				.WithData("roomNo", roomNo);
		}

		// Check for overlapping slots in the same academic year and room
		await ValidateNoOverlappingSlotAsync(academicYearId, startDate, endDate, roomNo);

		return new EntranceExamSlot(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			slotName,
			startDate,
			endDate,
			studentLimit,
			roomNo
		);
	}

	/// <summary>
	/// Updates an existing entrance exam slot with validation
	/// </summary>
	public async Task UpdateAsync(
		EntranceExamSlot slot,
		string? slotName,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
	{
		// Validate business rules
		if (startDate >= endDate)
		{
			throw new BusinessException("SM:EntranceExamSlot:001")
				.WithData("startDate", startDate)
				.WithData("endDate", endDate);
		}

		if (studentLimit <= 0)
		{
			throw new BusinessException("SM:EntranceExamSlot:002")
				.WithData("studentLimit", studentLimit);
		}

		if (roomNo <= 0)
		{
			throw new BusinessException("SM:EntranceExamSlot:003")
				.WithData("roomNo", roomNo);
		}

		// Check for overlapping slots in the same room (excluding current slot)
		await ValidateNoOverlappingSlotAsync(slot.AcademicYearId, startDate, endDate, roomNo, slot.Id);

		slot.Update(slotName, startDate, endDate, studentLimit, roomNo);
	}

	/// <summary>
	/// Validates that there is no exam slot overlapping with the specified start and end dates within the same academic year and room.
	/// If an overlapping slot is found (optionally excluding the slot with excludingSlotId), a BusinessException is thrown.
	/// </summary>
	/// <param name="academicYearId">The academic year to check for overlaps.</param>
	/// <param name="startDate">The start time of the exam slot.</param>
	/// <param name="endDate">The end time of the exam slot.</param>
	/// <param name="roomNo">The room number to check for overlaps.</param>
	/// <param name="excludingSlotId">Optional slot id to exclude from the check (used during updates).</param>
	public async Task ValidateNoOverlappingSlotAsync(
		Guid academicYearId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int roomNo,
		Guid? excludingSlotId = null)
	{
		var candidateSlots = await entranceExamSlotRepository.GetListAsync(slot =>
			slot.AcademicYearId == academicYearId &&
			slot.RoomNo == roomNo);

		var existingSlot = candidateSlots.FirstOrDefault(slot =>
			(!excludingSlotId.HasValue || slot.Id != excludingSlotId.Value) &&
			slot.StartDate < endDate &&
			startDate < slot.EndDate);

		if (existingSlot != null)
		{
			throw new BusinessException("SM:EntranceExamSlot:004")
				.WithData("existingSlotId", existingSlot.Id)
				.WithData("existingSlotName", existingSlot.SlotName!)
				.WithData("existingStartDate", existingSlot.StartDate)
				.WithData("existingEndDate", existingSlot.EndDate)
				.WithData("requestedStartDate", startDate)
				.WithData("requestedEndDate", endDate)
				.WithData("roomNo", roomNo);
		}
	}

	/// <summary>
	/// Validates that the academic year exists in the system.
	/// </summary>
	/// <param name="academicYearId">The academic year ID to validate.</param>
	/// <exception cref="BusinessException">Thrown when the academic year does not exist.</exception>
	private async Task ValidateAcademicYearExistsAsync(Guid academicYearId)
	{
		if (!await academicYearRepository.AnyAsync(ay => ay.Id == academicYearId))
		{
			throw new BusinessException("SM:EntranceExamSlot:007")
				.WithData("academicYearId", academicYearId);
		}
	}
}
