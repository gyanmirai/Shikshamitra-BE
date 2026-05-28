using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

[RemoteService(false)]
public class ExamSeatAllocationAppService(
    IRepository<ExamSeatAllocation, Guid> seatAllocationRepository,
    IRepository<Exam, Guid> examRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<ExamType, Guid> examTypeRepository,
    IGuidGenerator guidGenerator
) : ApplicationService, IExamSeatAllocationAppService
{
    public async Task<SeatLayoutResponseDto> GetSeatLayoutAsync(Guid examId, GetSeatLayoutInput input)
    {
        var room = await roomRepository.FindAsync(input.RoomId)
            ?? throw new UserFriendlyException("Room not found.");

        var totalSeats = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;

        // All seat allocations for this exam in this room
        var allAllocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && a.RoomId == input.RoomId);

        // Students lookup
        var studentIds = allAllocations.Select(a => a.StudentId).Distinct().ToList();
        var students = studentIds.Count > 0
            ? await studentRepository.GetListAsync(s => studentIds.Contains(s.Id))
            : new List<Student>();
        var studentsDict = students.ToDictionary(s => s.Id);

        // Standard lookup for each student
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : new List<Standard>();
        var standardsDict = standards.ToDictionary(s => s.Id);

        // Build seat items — one per seat in the room
        var allocBySeats = allAllocations.ToDictionary(a => a.SeatNumber);
        var allItems = new List<SeatLayoutItemDto>();

        for (var seat = 1; seat <= totalSeats; seat++)
        {
            allocBySeats.TryGetValue(seat, out var alloc);
            Student? student = alloc != null && studentsDict.TryGetValue(alloc.StudentId, out var s) ? s : null;
            var stdName = student?.StandardId != null && standardsDict.TryGetValue(student.StandardId!.Value, out var std)
                ? std.Name : string.Empty;

            allItems.Add(new SeatLayoutItemDto
            {
                SeatNumber = seat,
                StudentId = alloc?.StudentId,
                StudentName = student != null ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim() : null,
                StandardName = stdName,
                RollNo = student?.RollNo,
                RoomNumber = room.RoomName,
                IsRoomAssign = alloc?.IsRoomAssign ?? false,
                BenchId = alloc?.BenchId ?? Guid.Empty
            });
        }

        // Apply search
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            allItems = allItems.Where(i =>
                (i.StudentName?.ToLower().Contains(search) ?? false) ||
                (i.StandardName?.ToLower().Contains(search) ?? false)).ToList();
        }

        var totalCount = allItems.Count;
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var page = Math.Max(1, input.Page);
        var pagedItems = allItems.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatLayoutResponseDto
        {
            RoomId = input.RoomId,
            Room = ObjectMapper.Map<Room, RoomDto>(room),
            TotalSeats = totalSeats,
            AllocatedCount = allAllocations.Count,
            Items = pagedItems,
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<SeatStudentPagedResponseDto> GetStudentsForDivisionAsync(Guid examId, Guid divisionId, GetSeatStudentsInput input)
    {
        var exam = await examRepository.GetAsync(examId);

        var studentsQuery = await studentRepository.GetQueryableAsync();
        var query = studentsQuery.Where(s => s.DivisionId == divisionId && s.StandardId == exam.StandardId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            query = query.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(search)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(search)));
        }

        var students = await AsyncExecuter.ToListAsync(query);

        // Check which students already have seat allocations for this exam
        var studentIds = students.Select(s => s.Id).ToList();
        var allocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && studentIds.Contains(a.StudentId));
        var allocatedSet = allocations.ToDictionary(a => a.StudentId);

        // Standard name lookup
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : new List<Standard>();
        var standardsDict = standards.ToDictionary(s => s.Id);

        var items = students.Select(s =>
        {
            allocatedSet.TryGetValue(s.Id, out var alloc);
            var stdName = s.StandardId != null && standardsDict.TryGetValue(s.StandardId!.Value, out var std)
                ? std.Name : string.Empty;
            return new SeatStudentDto
            {
                StudentId = s.Id,
                StudentName = $"{s.FirstName} {s.MiddleName} {s.LastName}".Replace("  ", " ").Trim(),
                StandardName = stdName,
                RollNo = s.RollNo ?? 0,
                SeatNumber = alloc?.SeatNumber ?? 0,
                IsSeatAllocate = alloc != null
            };
        }).ToList();

        var totalCount = items.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var pagedItems = items.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatStudentPagedResponseDto
        {
            Items = pagedItems,
            PageNumber = page,
            PageSize = input.PageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

    public async Task<SaveSeatAllocationResponseDto> SaveSeatAllocationAsync(Guid examId, SaveSeatAllocationRequestDto input)
    {
        var existingAllocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && a.RoomId == input.RoomId);
        var existingByStudent = existingAllocations.ToDictionary(a => a.StudentId);

        var toInsert = new List<ExamSeatAllocation>();
        var toUpdate = new List<ExamSeatAllocation>();

        foreach (var student in input.Students)
        {
            if (existingByStudent.TryGetValue(student.StudentId, out var existing))
            {
                existing.Update(input.RoomId, student.SeatNumber, student.BenchId, student.IsRoomAssign);
                toUpdate.Add(existing);
            }
            else
            {
                toInsert.Add(new ExamSeatAllocation(
                    guidGenerator.Create(),
                    CurrentTenant.Id,
                    examId,
                    student.StudentId,
                    input.RoomId,
                    student.SeatNumber,
                    student.BenchId,
                    student.IsRoomAssign));
            }
        }

        // Delete allocations for students that are no longer in the submitted list (removed from seat)
        var incomingStudentIds = input.Students.Select(s => s.StudentId).ToHashSet();
        var toDelete = existingAllocations
            .Where(a => !incomingStudentIds.Contains(a.StudentId))
            .ToList();

        if (toInsert.Count > 0)
            await seatAllocationRepository.InsertManyAsync(toInsert, autoSave: true);
        if (toUpdate.Count > 0)
            await seatAllocationRepository.UpdateManyAsync(toUpdate, autoSave: true);
        if (toDelete.Count > 0)
            await seatAllocationRepository.DeleteManyAsync(toDelete, autoSave: true);

        return new SaveSeatAllocationResponseDto
        {
            ExamId = examId,
            RoomId = input.RoomId,
            SavedCount = toInsert.Count + toUpdate.Count
        };
    }

    public async Task<SeatAllocationDivisionsResponseDto> GetDivisionsForExamAsync(Guid examId, GetExamDivisionsInput input)
    {
        var exam = await examRepository.GetAsync(examId, includeDetails: true);

        var divisionIds = exam.ExamDivisions.Select(d => d.DivisionId).ToList();
        var divisions = await divisionRepository.GetListAsync(d =>
            divisionIds.Contains(d.Id) && d.StandardId == input.StandardId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            divisions = divisions.Where(d => d.Name.ToLower().Contains(search)).ToList();
        }

        var totalCount = divisions.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = divisions.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatAllocationDivisionsResponseDto
        {
            ExamId = examId,
            Items = paged.Select(d => new DivisionLiteDto { Id = d.Id, Name = d.Name }).ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<SeatAllocationDashboardResponseDto> GetSeatAllocationTableAsync(GetSeatAllocationTableInput input)
    {
        var examQuery = await examRepository.GetQueryableAsync();
        var exams = await AsyncExecuter.ToListAsync(
            examQuery.Where(e => e.StandardId == input.StandardId && e.Status == ExamStatus.Published));

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            exams = exams.Where(e => e.ExamName.ToLower().Contains(search)).ToList();
        }

        // Fetch supporting data
        var examIds = exams.Select(e => e.Id).ToList();
        var examTypeIds = exams.Select(e => e.ExamTypeId).Distinct().ToList();
        var examTypes = await examTypeRepository.GetListAsync(t => examTypeIds.Contains(t.Id));
        var examTypesDict = examTypes.ToDictionary(t => t.Id);

        var standard = await standardRepository.FindAsync(input.StandardId);

        // Get all division IDs used by these exams
        var allExamsWithDetails = await examRepository.GetListAsync(
            e => examIds.Contains(e.Id), includeDetails: true);
        var allDivisionIds = allExamsWithDetails
            .SelectMany(e => e.ExamDivisions.Select(d => d.DivisionId))
            .Distinct().ToList();
        var divisions = allDivisionIds.Count > 0
            ? await divisionRepository.GetListAsync(d => allDivisionIds.Contains(d.Id))
            : new List<Division>();
        var divisionsDict = divisions.ToDictionary(d => d.Id);

        // Students per division — needed to compute accurate per-exam totals
        var studentsInDivisions = allDivisionIds.Count > 0
            ? await studentRepository.GetListAsync(s =>
                s.StandardId == input.StandardId &&
                s.DivisionId.HasValue &&
                allDivisionIds.Contains(s.DivisionId!.Value))
            : new List<Student>();
        var studentCountByDivision = studentsInDivisions
            .Where(s => s.DivisionId.HasValue)
            .GroupBy(s => s.DivisionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Allocations per exam
        var allocCounts = await seatAllocationRepository.GetListAsync(a => examIds.Contains(a.ExamId));
        var allocByExam = allocCounts.GroupBy(a => a.ExamId).ToDictionary(g => g.Key, g => g.Count());

        var items = allExamsWithDetails.Select(exam =>
        {
            examTypesDict.TryGetValue(exam.ExamTypeId, out var examType);
            var divisionNames = exam.ExamDivisions
                .Where(d => divisionsDict.ContainsKey(d.DivisionId))
                .Select(d => divisionsDict[d.DivisionId].Name)
                .ToList();
            var examDivisionIds = exam.ExamDivisions.Select(d => d.DivisionId).ToList();
            var totalStudents = examDivisionIds.Sum(divId => studentCountByDivision.GetValueOrDefault(divId, 0));
            var allocated = allocByExam.GetValueOrDefault(exam.Id, 0);
            var pending = totalStudents - allocated;
            var status = allocated == 0 ? 0 : (allocated < totalStudents ? 1 : 2);

            return new SeatAllocationSummaryDto
            {
                ExamId = exam.Id,
                ExamName = exam.ExamName,
                ExamTypeName = examType?.Name ?? string.Empty,
                StandardName = standard?.Name ?? string.Empty,
                Divisions = divisionNames,
                TotalStudents = totalStudents,
                AllocatedStudents = allocated,
                PendingStudents = Math.Max(0, pending),
                Status = status
            };
        }).ToList();

        var totalCount = items.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = items.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatAllocationDashboardResponseDto
        {
            Items = paged,
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<ExamRoomPagedResponseDto> GetRoomsForExamAndDivisionAsync(Guid examId, Guid divisionId, GetExamRoomsInput input)
    {
        var exam = await examRepository.GetAsync(examId, includeDetails: true);

        // Collect all room IDs assigned to this division across subjects
        var roomIds = exam.ExamSubjects
            .SelectMany(s => s.RoomAssignments)
            .Where(ra => ra.DivisionId == divisionId)
            .SelectMany(ra => ra.RoomIds)
            .Distinct()
            .ToList();

        var rooms = roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : new List<Room>();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            rooms = rooms.Where(r => r.RoomName.ToLower().Contains(search)).ToList();
        }

        // Allocation counts per room for this exam
        var allocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && roomIds.Contains(a.RoomId));
        var allocByRoom = allocations.GroupBy(a => a.RoomId).ToDictionary(g => g.Key, g => g.Count());

        var totalCount = rooms.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = rooms.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        var items = paged.Select(r =>
        {
            var capacity = r.TotalBenches > 0 ? r.TotalBenches * r.BenchCapacity : r.Capacity;
            var allocated = allocByRoom.GetValueOrDefault(r.Id, 0);
            return new ExamRoomDto
            {
                Id = r.Id,
                RoomName = r.RoomName,
                RoomNumber = r.RoomName,
                Capacity = capacity,
                AllocatedSeats = allocated,
                AvailableSeats = Math.Max(0, capacity - allocated),
                IsAllocationCompleted = allocated >= capacity
            };
        }).ToList();

        return new ExamRoomPagedResponseDto
        {
            Items = items,
            CurrentPage = page,
            PageSize = input.PageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }
}
