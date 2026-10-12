using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileResultAppService(MobileAcademicContextAppService context, MobileExamAppService exams,
	IRepository<ExamResultHeader, Guid> headers, IRepository<ExamResultDetail, Guid> details,
	IRepository<ExamMark, Guid> marks) : ApplicationService, IMobileResultAppService
{
	public async Task<List<MobileYearDto>> GetYearsAsync(MobileAcademicInput input) =>
		(await context.GetAsync(input)).AvailableAcademicYears;

	public async Task<MobilePage<MobileResultDto>> GetListAsync(MobileListInput input)
	{
		var p = await context.ResolveAsync(input);
		var examRows = await exams.ReadDtosAsync(p);
		var examIds = examRows.Select(x => x.Id).ToArray();
		var published = await headers.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.DivisionId == p.DivisionId && Enumerable.Contains(examIds, x.ExamId) &&
			(x.Status == ExamResultStatus.Published || x.Status == ExamResultStatus.Frozen));
		var headerIds = published.Select(x => x.Id).ToArray();
		var snapshots = await details.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && Enumerable.Contains(headerIds, x.ResultHeaderId));
		var rows = examRows.Where(x => x.Status == "completed" || published.Any(h => h.ExamId == x.Id)).Select(e =>
		{
			var header = published.SingleOrDefault(x => x.ExamId == e.Id);
			var detail = header == null ? null : snapshots.SingleOrDefault(x => x.ResultHeaderId == header.Id);
			var released = detail != null && detail.ResultStatus != ExamStudentResultStatus.Withheld;
			return new MobileResultDto { Id = header?.Id ?? e.Id, ExamId = e.Id, ExamTitle = e.Title, AcademicYear = p.Year.Name,
				StudentName = MobileAcademicContextAppService.Name(p.Student), GradeSection = $"{p.StandardName} - {p.DivisionName}",
				ExamDate = e.Schedule.FirstOrDefault()?.ExamDate, PublishedDate = released ? header?.PublishedOn : null,
				Status = released ? "published" : detail?.ResultStatus == ExamStudentResultStatus.Withheld ? "unavailable" : "pending",
				OverallObtained = released ? detail!.TotalObtainedMarks : null, OverallTotal = released ? detail!.TotalMaxMarks : null,
				Percentage = released ? detail!.Percentage : null, Grade = released ? detail!.Grade : null, PassStatus = released ? detail!.ResultStatus.ToString() : null,
				Rank = released ? detail!.ClassRank : null };
		});
		if (!string.IsNullOrWhiteSpace(input.Search)) rows = rows.Where(x => x.ExamTitle.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrEmpty(input.Status)) rows = rows.Where(x => x.Status == input.Status);
		return MobileAcademicContextAppService.Page(rows, input, p);
	}

	public async Task<MobileResultDto> GetAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		MobileResultDto? dto = null;
		for (var page = 1; dto == null; page++) {
			var result = await GetListAsync(new() { StudentId = p.Student.Id, AcademicYearId = p.Year.Id, PageSize = 100, PageNumber = page });
			dto = result.Items.SingleOrDefault(x => x.Id == id);
			if (page * 100 >= result.TotalCount) break;
		}
		dto = dto
			?? throw new EntityNotFoundException(typeof(ExamResultHeader), id);
		if (dto.Status != "published") return dto;
		var exam = await exams.GetAsync(dto.ExamId, input);
		var publishedMarks = await marks.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && x.ExamId == dto.ExamId && x.IsPublished && x.DivisionId == p.DivisionId);
		var releasedStudents = (await details.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ResultHeaderId == dto.Id && x.DivisionId == p.DivisionId && x.ResultStatus != ExamStudentResultStatus.Withheld)).Select(x => x.StudentId).ToArray();
		var classMarks = await marks.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ExamId == dto.ExamId && x.IsPublished && x.DivisionId == p.DivisionId && !x.IsAbsent && x.MarksObtained.HasValue && Enumerable.Contains(releasedStudents, x.StudentId));
		dto.Subjects = exam.Schedule.Select(s =>
		{
			var mark = publishedMarks.SingleOrDefault(x => x.SubjectId == s.SubjectId);
			var scores = classMarks.Where(x => x.SubjectId == s.SubjectId && s.TotalMarks > 0).Select(x => x.MarksObtained!.Value / s.TotalMarks!.Value * 100).ToList();
			return new MobileSubjectResultDto(s.SubjectId, s.SubjectName, mark?.MarksObtained, s.TotalMarks,
				mark?.MarksObtained != null && s.TotalMarks > 0 ? Math.Round(mark.MarksObtained.Value / s.TotalMarks.Value * 100, 2) : null,
				mark == null ? null : mark.IsAbsent ? "absent" : s.PassingMarks.HasValue && mark.MarksObtained.HasValue ? mark.MarksObtained >= s.PassingMarks ? "pass" : "fail" : null, mark?.Feedback,
				scores.Count == 0 ? null : Math.Round(scores.Average(), 2), scores.Count == 0 ? null : Math.Round(scores.Max(), 2));
		}).ToList();
		return dto;
	}
	public async Task<MobileSubjectResultDto> GetSubjectAsync(Guid id, Guid subjectId, MobileAcademicInput input) =>
		(await GetAsync(id, input)).Subjects.SingleOrDefault(x => x.SubjectId == subjectId) ?? throw new EntityNotFoundException(typeof(ExamMark), subjectId);

	public async Task<MobileFileDto> DownloadReportCardAsync(Guid id, MobileAcademicInput input)
	{
		var result = await GetAsync(id, input);
		if (result.Status != "published") throw MobileAcademicContextAppService.Invalid("The report card is not published.");
		var p = await context.ResolveAsync(input);
		var bytes = Document.Create(document => document.Page(page =>
		{
			page.Margin(36);
			page.DefaultTextStyle(x => x.FontSize(11));
			page.Header().Text(CurrentTenant.Name ?? "School").FontSize(18).Bold();
			page.Content().Column(column =>
			{
				column.Spacing(12);
				column.Item().Text($"{result.ExamTitle} | {result.AcademicYear}").Bold();
				column.Item().Text($"{MobileAcademicContextAppService.Name(p.Student)} | {p.StandardName} - {p.DivisionName}");
				column.Item().Table(table =>
				{
					table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
					table.Header(h => { foreach (var title in new[] { "Subject", "Marks", "Maximum", "Status" }) h.Cell().Padding(5).Text(title).Bold(); });
					foreach (var subject in result.Subjects)
					{
						table.Cell().Padding(5).Text(subject.SubjectName);
						table.Cell().Padding(5).Text(subject.ObtainedMarks?.ToString("0.##") ?? "Unavailable");
						table.Cell().Padding(5).Text(subject.TotalMarks?.ToString("0.##") ?? "Unavailable");
						table.Cell().Padding(5).Text(subject.Status ?? "Unavailable");
					}
				});
				column.Item().Text($"Total: {result.OverallObtained:0.##}/{result.OverallTotal:0.##} | {result.Percentage:0.##}% | Grade: {result.Grade}").Bold();
				column.Item().Text($"Result: {result.PassStatus} | Published: {result.PublishedDate:yyyy-MM-dd}");
			});
			page.Footer().AlignCenter().Text(t => { t.Span("Page "); t.CurrentPageNumber(); });
		})).GeneratePdf();
		return new(bytes, $"report-card-{result.ExamId:N}.pdf", "application/pdf");
	}
}
