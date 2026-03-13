using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

[Authorize]
public class EntranceExamSlotAppService : ShikshaMitraAppService, IEntranceExamSlotAppService
{
	private readonly IRepository<EntranceExamSlot, Guid> _slotRepository;
	private readonly IRepository<StudentAdditionalDetail, Guid> _studentAdditionalDetailRepository;
	private readonly EntranceExamSlotManager _slotManager;

	public EntranceExamSlotAppService(
		IRepository<EntranceExamSlot, Guid> slotRepository,
		IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
		EntranceExamSlotManager slotManager)
	{
		_slotRepository = slotRepository;
		_studentAdditionalDetailRepository = studentAdditionalDetailRepository;
		_slotManager = slotManager;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<PagedResultDto<EntranceExamSlotDto>> GetListAsync(GetEntranceExamSlotsInput input)
	{
		// Get queryable
		var queryable = await _slotRepository.GetQueryableAsync();

		// Apply filters
		if (input.AcademicYearId.HasValue)
		{
			queryable = queryable.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		}

		if (!input.SearchTerm.IsNullOrWhiteSpace())
		{
			queryable = queryable.Where(x =>
				(x.SlotName != null && x.SlotName.Contains(input.SearchTerm)) ||
				x.RoomNo.ToString().Contains(input.SearchTerm));
		}

		if (input.StartDate.HasValue)
		{
			var startOfDay = input.StartDate.Value.Date;
			queryable = queryable.Where(x => x.StartDate.Date >= startOfDay);
		}

		if (input.EndDate.HasValue)
		{
			var endOfDay = input.EndDate.Value.Date.AddDays(1);
			queryable = queryable.Where(x => x.StartDate.Date < endOfDay);
		}

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Apply sorting
		if (!input.Sorting.IsNullOrWhiteSpace())
		{
			queryable = queryable.OrderBy(input.Sorting);
		}
		else
		{
			queryable = queryable.OrderBy(x => x.StartDate);
		}

		// Apply paging
		queryable = queryable
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		// Execute query and map to DTOs
		var slots = await AsyncExecuter.ToListAsync(queryable);
		var items = new System.Collections.Generic.List<EntranceExamSlotDto>();

		foreach (var slot in slots)
		{
			var enrolledCount = await GetEnrolledStudentsCountAsync(slot.Id);

			var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(slot);
			dto.EnrolledStudents = enrolledCount;
			dto.AvailableSeats = slot.StudentLimit - enrolledCount;
			items.Add(dto);
		}

		return new PagedResultDto<EntranceExamSlotDto>(totalCount, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<EntranceExamSlotDetailDto> GetAsync(Guid id)
	{
		var slot = await _slotRepository.GetAsync(id);
		var enrolledCount = await GetEnrolledStudentsCountAsync(id);

		var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDetailDto>(slot);
		dto.EnrolledStudents = enrolledCount;
		dto.AvailableSeats = slot.StudentLimit - enrolledCount;

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Add)]
	public async Task<EntranceExamSlotDto> CreateAsync(CreateEntranceExamSlotDto input)
	{
		// Combine date with times to create DateTimeOffset
		var startDateTime = input.Date.Date.Add(input.StartTime);
		var endDateTime = input.Date.Date.Add(input.EndTime);

		// Use manager to create with validation
		var slot = await _slotManager.CreateAsync(
			input.AcademicYearId,
			input.SlotName,
			new DateTimeOffset(startDateTime, TimeSpan.Zero),
			new DateTimeOffset(endDateTime, TimeSpan.Zero),
			input.StudentLimit,
			input.RoomNo
		);

		await _slotRepository.InsertAsync(slot, autoSave: true);

		var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(slot);
		dto.EnrolledStudents = 0;
		dto.AvailableSeats = slot.StudentLimit;

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Add)]
	public async Task<EntranceExamSlotDto> UpdateAsync(Guid id, UpdateEntranceExamSlotDto input)
	{
		var slot = await _slotRepository.GetAsync(id);

		// Check enrolled students count
		var enrolledCount = await GetEnrolledStudentsCountAsync(id);
		if (input.StudentLimit < enrolledCount)
		{
			throw new BusinessException("SM:EntranceExamSlot:005")
				.WithData("studentLimit", input.StudentLimit)
				.WithData("enrolledCount", enrolledCount);
		}

		// Combine date with times
		var startDateTime = input.Date.Date.Add(input.StartTime);
		var endDateTime = input.Date.Date.Add(input.EndTime);

		// Use manager to update with validation
		await _slotManager.UpdateAsync(
			slot,
			input.SlotName,
			new DateTimeOffset(startDateTime, TimeSpan.Zero),
			new DateTimeOffset(endDateTime, TimeSpan.Zero),
			input.StudentLimit,
			input.RoomNo
		);

		await _slotRepository.UpdateAsync(slot, autoSave: true);

		var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(slot);
		dto.EnrolledStudents = enrolledCount;
		dto.AvailableSeats = slot.StudentLimit - enrolledCount;

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		// Check if any students are enrolled
		var enrolledCount = await GetEnrolledStudentsCountAsync(id);
		if (enrolledCount > 0)
		{
			throw new BusinessException("SM:EntranceExamSlot:006")
				.WithData("enrolledCount", enrolledCount);
		}

		await _slotRepository.DeleteAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<PagedResultDto<AvailableExamSlotDto>> GetAvailableSlotsAsync(GetAvailableExamSlotsInput input)
	{
		var queryable = await _slotRepository.GetQueryableAsync();

		// Apply filters
		if (input.AcademicYearId.HasValue)
		{
			queryable = queryable.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		}

		if (input.Date.HasValue)
		{
			var targetDate = input.Date.Value.Date;
			queryable = queryable.Where(x => x.StartDate.Date == targetDate);
		}

		// Get slots with their enrolled counts
		var slots = await AsyncExecuter.ToListAsync(queryable);
		var availableSlots = new System.Collections.Generic.List<AvailableExamSlotDto>();

		foreach (var slot in slots)
		{
			var enrolledCount = await GetEnrolledStudentsCountAsync(slot.Id);
			var availableSeats = slot.StudentLimit - enrolledCount;

			// Only include slots with available capacity
			if (availableSeats > 0 &&
			    (!input.MinAvailableSeats.HasValue || availableSeats >= input.MinAvailableSeats.Value))
			{
				var dto = ObjectMapper.Map<EntranceExamSlot, AvailableExamSlotDto>(slot);
				dto.AvailableSeats = availableSeats;

				// Format display text
				var slotNamePart = !string.IsNullOrWhiteSpace(slot.SlotName) ? $"{slot.SlotName} - " : "";
				var datePart = slot.StartDate.ToString("MMM dd, yyyy");
				var timePart = $"({slot.GetStartTime():hh\\:mm} - {slot.GetEndTime():hh\\:mm})";
				dto.DisplayText = $"{slotNamePart}{datePart} {timePart} - Room {slot.RoomNo} - {availableSeats} seats available";

				availableSlots.Add(dto);
			}
		}

		// Sort by date and time
		availableSlots = availableSlots
			.OrderBy(x => x.Date)
			.ThenBy(x => x.StartTime)
			.ToList();

		// Apply paging
		var totalCount = availableSlots.Count;
		var pagedItems = availableSlots
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		return new PagedResultDto<AvailableExamSlotDto>(totalCount, pagedItems);
	}

	private async Task<int> GetEnrolledStudentsCountAsync(Guid slotId)
	{
		var queryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(
			queryable.Where(sd => sd.EntranceExamSlotId == slotId));
	}
}
