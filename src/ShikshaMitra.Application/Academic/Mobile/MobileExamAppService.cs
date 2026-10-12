using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileExamAppService(MobileAcademicContextAppService context, IRepository<Exam, Guid> exams,
	IRepository<Subject, Guid> subjects, IRepository<ExamResultHeader, Guid> results,
	IRepository<ExamSeatAllocation, Guid> seats, IRepository<ExamSeatingPlanSubject, Guid> planSubjects,
	IRepository<ExamSeatingPublication, Guid> publications, IRepository<Room, Guid> rooms,
	MobileSyllabusAppService syllabus, MobileAttachmentStore files) : ApplicationService, IMobileExamAppService
{
	public async Task<List<Exam>> ReadExamsAsync(MobilePlacement p)
	{
		var query = (await exams.WithDetailsAsync(x => x.ExamSubjects, x => x.ExamDivisions)).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == p.StandardId && (x.Status == ExamStatus.Published || x.Status == ExamStatus.Completed) &&
			x.ExamDivisions.Any(d => d.DivisionId == p.DivisionId));
		return await AsyncExecuter.ToListAsync(query);
	}

	public async Task<List<MobileExamDto>> ReadDtosAsync(MobilePlacement p)
	{
		var assigned = (await context.GetSubjectsAsync(p)).Select(x => x.SubjectId).ToHashSet();
		var list = await ReadExamsAsync(p);
		var ids = list.SelectMany(x => x.ExamSubjects).Select(x => x.SubjectId).Distinct().ToArray();
		var names = (await subjects.GetListAsync(x => Enumerable.Contains(ids, x.Id))).ToDictionary(x => x.Id, x => x.Name);
		var examIds = list.Select(x => x.Id).ToArray();
		var headers = await results.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(examIds, x.ExamId) && x.DivisionId == p.DivisionId &&
			(x.Status == ExamResultStatus.Published || x.Status == ExamResultStatus.Frozen));
		return list.Select(e =>
		{
			var schedule = e.ExamSubjects.Where(x => assigned.Contains(x.SubjectId)).OrderBy(x => x.ExamDate).ThenBy(x => x.StartTime)
				.Select(x => new MobileExamSubjectDto { Id = x.Id, SubjectId = x.SubjectId, SubjectName = names.GetValueOrDefault(x.SubjectId, ""), ExamDate = x.ExamDate,
					StartTime = x.StartTime, EndTime = x.EndTime, TotalMarks = x.TotalMarks, PassingMarks = x.PassingMarks, Notes = x.Notes }).ToList();
			var first = schedule.Where(x => x.ExamDate.HasValue).Select(x => x.ExamDate!.Value.Date).DefaultIfEmpty(p.Year.EndDate.Date).Min();
			var last = schedule.Where(x => x.ExamDate.HasValue).Select(x => x.ExamDate!.Value.Date).DefaultIfEmpty(p.Year.EndDate.Date).Max();
			var today = MobileAcademicContextAppService.SchoolToday;
			var status = e.Status == ExamStatus.Completed || last < today ? "completed" : first <= today ? "ongoing" : "upcoming";
			return new MobileExamDto(e.Id, e.ExamName, p.Year.Name, $"{p.StandardName} - {p.DivisionName}", status,
				headers.Any(x => x.ExamId == e.Id) ? "published" : "pending", Math.Max(0, (first - today).Days), schedule);
		}).OrderBy(x => x.Schedule.FirstOrDefault()?.ExamDate).ThenBy(x => x.Id).ToList();
	}

	public async Task<MobilePage<MobileExamDto>> GetListAsync(MobileListInput input)
	{
		var p = await context.ResolveAsync(input);
		var rows = (await ReadDtosAsync(p)).Where(x => string.IsNullOrEmpty(input.Status) || x.Status == input.Status);
		if (!string.IsNullOrWhiteSpace(input.Search)) rows = rows.Where(x => x.Title.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase));
		return MobileAcademicContextAppService.Page(rows, input, p);
	}
	public async Task<MobileExamDto> GetAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		return (await ReadDtosAsync(p)).SingleOrDefault(x => x.Id == id) ?? throw new EntityNotFoundException(typeof(Exam), id);
	}
	public async Task<MobileExamSubjectDto> GetPreparationAsync(Guid id, Guid scheduleId, MobileAcademicInput input) =>
		(await GetAsync(id, input)).Schedule.SingleOrDefault(x => x.Id == scheduleId) ?? throw new EntityNotFoundException(typeof(ExamSubject), scheduleId);

	public async Task<MobileSeatingDto> GetSeatingAsync(Guid id, Guid scheduleId, MobileAcademicInput input)
	{
		await GetPreparationAsync(id, scheduleId, input);
		var p = await context.ResolveAsync(input);
		if (!await publications.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.ExamId == id)) return new("notPublished", null, null, null, p.RollNumber);
		var plans = (await planSubjects.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ExamId == id && x.ExamSubjectId == scheduleId)).Select(x => x.SeatingPlanId).ToArray();
		var seat = await seats.FindAsync(x => x.TenantId == CurrentTenant.Id && x.ExamId == id && x.StudentId == p.Student.Id && Enumerable.Contains(plans, x.SeatingPlanId));
		if (seat == null) return new("notAllocated", null, null, null, p.RollNumber);
		var room = await rooms.GetAsync(seat.RoomId);
		return new("available", room.RoomName, seat.SeatNumber, null, p.RollNumber);
	}

	public async Task<List<MobileAttachmentDto>> GetResourcesAsync(Guid id, Guid scheduleId, MobileAcademicInput input, string type)
	{
		var subject = await GetPreparationAsync(id, scheduleId, input);
		var p = await context.ResolveAsync(input);
		// Only explicitly classified, published resources are preparation material. No inferred exam portion.
		return (await syllabus.ReadResourcesAsync(p, subject.SubjectId)).Where(x => x.ResourceType.Equals(type, StringComparison.OrdinalIgnoreCase))
			.Select(x => new MobileAttachmentDto(x.Id, x.Title, $"/api/mobile/v1/exams/{id}/subjects/{scheduleId}/resources/{x.Id}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}", x.ContentType, x.Size, x.FileName)).ToList();
	}
	public async Task<MobileFileDto> DownloadAsync(Guid id, Guid scheduleId, Guid resourceId, MobileAcademicInput input)
	{
		var subject = await GetPreparationAsync(id, scheduleId, input);
		var p = await context.ResolveAsync(input);
		var resource = (await syllabus.ReadResourcesAsync(p, subject.SubjectId)).SingleOrDefault(x => x.Id == resourceId)
			?? throw new EntityNotFoundException(typeof(LearningResource), resourceId);
		return await files.DownloadPublishedAsync(resource.BlobName, resource.FileName, resource.ContentType);
	}
}
