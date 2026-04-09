using System;
using System.Collections.Generic;
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
[RemoteService(false)]
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
		var queryable = await _slotRepository.GetQueryableAsync();

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

		var totalCount = await AsyncExecuter.CountAsync(queryable);

		if (!input.Sorting.IsNullOrWhiteSpace())
		{
			queryable = queryable.OrderBy(input.Sorting);
		}
		else
		{
			queryable = queryable.OrderBy(x => x.StartDate);
		}

		queryable = queryable.Skip(input.SkipCount).Take(input.MaxResultCount);

		var slots = await AsyncExecuter.ToListAsync(queryable);

		// Batch-load enrolled counts in a single query instead of N+1 round-trips
		var slotIds = slots.Select(s => s.Id).ToList();
		var enrolledCountMap = await GetEnrolledCountsForSlotsAsync(slotIds);

		var items = slots.Select(slot =>
		{
			var enrolledCount = enrolledCountMap.GetValueOrDefault(slot.Id, 0);
			var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(slot);
			dto.EnrolledStudents = enrolledCount;
			dto.AvailableSeats = slot.StudentLimit - enrolledCount;
			return dto;
		}).ToList();

		return new PagedResultDto<EntranceExamSlotDto>(totalCount, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<EntranceExamSlotDetailDto> GetAsync(Guid id)
	{
		var slot = await _slotRepository.GetAsync(id);
		var enrolledCount = await GetEnrolledCountForSlotAsync(id);

		var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDetailDto>(slot);
		dto.EnrolledStudents = enrolledCount;
		dto.AvailableSeats = slot.StudentLimit - enrolledCount;

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Add)]
	public async Task<EntranceExamSlotDto> CreateAsync(CreateEntranceExamSlotDto input)
	{
		var slot = await _slotManager.CreateAsync(
			input.AcademicYearId,
			input.SlotName,
			input.StartTimeDate,
			input.EndTimeDate,
			input.StudentLimit,
			input.RoomNo);

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

		var enrolledCount = await GetEnrolledCountForSlotAsync(id);
		if (input.StudentLimit < enrolledCount)
		{
			throw new BusinessException("SM:EntranceExamSlot:005")
				.WithData("studentLimit", input.StudentLimit)
				.WithData("enrolledCount", enrolledCount);
		}

		var startDateTime = input.Date.Date.Add(input.StartTime);
		var endDateTime = input.Date.Date.Add(input.EndTime);

		await _slotManager.UpdateAsync(
			slot,
			input.SlotName,
			new DateTimeOffset(startDateTime, TimeSpan.Zero),
			new DateTimeOffset(endDateTime, TimeSpan.Zero),
			input.StudentLimit,
			input.RoomNo);

		await _slotRepository.UpdateAsync(slot, autoSave: true);

		var dto = ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(slot);
		dto.EnrolledStudents = enrolledCount;
		dto.AvailableSeats = slot.StudentLimit - enrolledCount;

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var enrolledCount = await GetEnrolledCountForSlotAsync(id);
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
		var slotQueryable = await _slotRepository.GetQueryableAsync();
		var detailQueryable = await _studentAdditionalDetailRepository.GetQueryableAsync();

		// Compute enrolled count per slot as a DB-side subquery — avoids N+1 and in-memory paging
		var enrolledPerSlot = detailQueryable
			.Where(sd => sd.EntranceExamSlotId != null)
			.GroupBy(sd => sd.EntranceExamSlotId!.Value)
			.Select(g => new { SlotId = g.Key, Count = g.Count() });

		var availableQuery =
			from slot in slotQueryable
			join e in enrolledPerSlot on slot.Id equals e.SlotId into eGroup
			from e in eGroup.DefaultIfEmpty()
			let enrolled = e == null ? 0 : e.Count
			let available = slot.StudentLimit - enrolled
			where available > 0
			select new { slot, enrolled, available };

		if (input.AcademicYearId.HasValue)
		{
			availableQuery = availableQuery.Where(x => x.slot.AcademicYearId == input.AcademicYearId.Value);
		}

		if (input.Date.HasValue)
		{
			var targetDate = input.Date.Value.Date;
			availableQuery = availableQuery.Where(x => x.slot.StartDate.Date == targetDate);
		}

		if (input.MinAvailableSeats.HasValue)
		{
			availableQuery = availableQuery.Where(x => x.available >= input.MinAvailableSeats.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(availableQuery);

		availableQuery = availableQuery
			.OrderBy(x => x.slot.StartDate)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		var results = await AsyncExecuter.ToListAsync(availableQuery);

		var items = results.Select(r =>
		{
			var dto = ObjectMapper.Map<EntranceExamSlot, AvailableExamSlotDto>(r.slot);
			dto.AvailableSeats = r.available;

			var slotNamePart = !string.IsNullOrWhiteSpace(r.slot.SlotName) ? $"{r.slot.SlotName} - " : string.Empty;
			var datePart = r.slot.StartDate.ToString("MMM dd, yyyy");
			var timePart = $"({r.slot.GetStartTime():hh\\:mm} - {r.slot.GetEndTime():hh\\:mm})";
			dto.DisplayText = $"{slotNamePart}{datePart} {timePart} - Room {r.slot.RoomNo} - {r.available} seats available";

			return dto;
		}).ToList();

		return new PagedResultDto<AvailableExamSlotDto>(totalCount, items);
	}

	// Single slot count — used for mutating operations (update, delete) where one call is fine
	private async Task<int> GetEnrolledCountForSlotAsync(Guid slotId)
	{
		var queryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(queryable.Where(sd => sd.EntranceExamSlotId == slotId));
	}

	// Batch count for a set of slot IDs — eliminates N+1 in list queries
	private async Task<Dictionary<Guid, int>> GetEnrolledCountsForSlotsAsync(List<Guid> slotIds)
	{
		if (slotIds.Count == 0)
		{
			return [];
		}

		var queryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		var counts = await AsyncExecuter.ToListAsync(
			queryable
				.Where(sd => sd.EntranceExamSlotId != null && slotIds.Contains(sd.EntranceExamSlotId!.Value))
				.GroupBy(sd => sd.EntranceExamSlotId!.Value)
				.Select(g => new { SlotId = g.Key, Count = g.Count() }));

		return counts.ToDictionary(x => x.SlotId, x => x.Count);
	}
}
