using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileSyllabusAppService(MobileAcademicContextAppService context,
	IRepository<AssignedSubjectChapter, Guid> chapters, IRepository<ChapterProgress, Guid> progress,
	IRepository<Subject, Guid> subjects, IRepository<LearningResource, Guid> resources,
	IRepository<AssignedSubjectDocument, Guid> documents, MobileAttachmentStore files)
	: ApplicationService, IMobileSyllabusAppService
{
	public async Task<List<MobileChapterDto>> ReadChaptersAsync(MobilePlacement p)
	{
		var assignments = await context.GetSubjectsAsync(p);
		var ids = assignments.Select(x => x.Id).ToArray();
		var list = await chapters.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.AssignedSubjectId) &&
			(x.PublishStatus == "Published" || x.PublishStatus == "published") && (x.Visibility == "Students" || p.IsGuardian && x.Visibility == "Parents" || x.Visibility == "All"));
		var chapterIds = list.Select(x => x.Id).ToArray();
		var coverage = await progress.GetListAsync(x => x.DivisionId == p.DivisionId && Enumerable.Contains(chapterIds, x.ChapterId));
		return list.OrderBy(x => x.ChapterNumber).ThenBy(x => x.Id).Select(c =>
		{
			var value = coverage.SingleOrDefault(x => x.ChapterId == c.Id)?.Progress ?? 0;
			return new MobileChapterDto { Id = c.Id, StandardId = p.StandardId, SubjectId = assignments.Single(x => x.Id == c.AssignedSubjectId).SubjectId,
				ChapterNumber = c.ChapterNumber, ChapterName = c.ChapterName, Description = c.Description, Topic = c.Topic, UnitName = c.UnitName,
				TermName = c.TermName, ChapterStartTime = c.ChapterStartTime, ChapterEndTime = c.ChapterEndTime, LearningOutcomes = c.LearningOutcomes,
				EstimatedTeachingDays = c.EstimatedTeachingDays, EstimatedPeriods = c.EstimatedPeriods, DifficultyLevel = c.DifficultyLevel,
				BloomLevel = c.BloomLevel, Tags = c.Tags, ClassActivities = c.ClassActivities, CompletionCriteria = c.CompletionCriteria,
				AssessmentMethod = c.AssessmentMethod, QuestionTypes = c.QuestionTypes, MarksWeightage = c.MarksWeightage,
				SkillsCovered = c.SkillsCovered, Prerequisites = c.Prerequisites, StudentSummary = c.StudentSummary, ParentSummary = p.IsGuardian ? c.ParentSummary : null,
				ProgressPercentage = value, CoverageStatus = value == 100 ? "completed" : value > 0 ? "inProgress" : "notStarted" };
		}).ToList();
	}

	public async Task<MobileSyllabusDto> GetDashboardAsync(MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var assigned = await context.GetSubjectsAsync(p);
		var rows = await ReadChaptersAsync(p);
		var ids = assigned.Select(x => x.SubjectId).ToArray();
		var subjectDetails = (await subjects.GetListAsync(x => Enumerable.Contains(ids, x.Id))).ToDictionary(x => x.Id);
		var summaries = assigned.Select(a =>
		{
			var group = rows.Where(c => c.SubjectId == a.SubjectId).ToList();
			var subject = subjectDetails.GetValueOrDefault(a.SubjectId);
			var summary = Summarize(group);
			return new MobileSubjectProgressDto(a.SubjectId, subject?.Name ?? "", a.SubjectCode,
				summary.CoveragePercentage, summary.TotalChapters, summary.CompletedChapters, summary.InProgressChapters, summary.UpcomingChapters, subject?.Color);
		}).ToList();
		var total = Summarize(rows);
		return new(p.Student.Id, p.Year.Id, MobileAcademicContextAppService.Name(p.Student), $"{p.StandardName} - {p.DivisionName}",
			total.CoveragePercentage, total.CompletedChapters, total.TotalChapters, summaries);
	}

	public async Task<MobileChapterPageDto> GetChaptersAsync(Guid subjectId, MobileListInput input)
	{
		var p = await context.ResolveAsync(input);
		if (!(await context.GetSubjectsAsync(p)).Any(x => x.SubjectId == subjectId)) throw new EntityNotFoundException(typeof(Subject), subjectId);
		var chaptersForSubject = (await ReadChaptersAsync(p)).Where(x => x.SubjectId == subjectId).ToList();
		var summary = Summarize(chaptersForSubject);
		IEnumerable<MobileChapterDto> rows = chaptersForSubject;
		if (!string.IsNullOrEmpty(input.Status)) rows = rows.Where(x => x.CoverageStatus == input.Status);
		if (!string.IsNullOrWhiteSpace(input.Search)) rows = rows.Where(x => x.ChapterName.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase));
		var page = MobileAcademicContextAppService.Page(rows, input, p);
		return new MobileChapterPageDto {
			StudentId = page.StudentId, AcademicYearId = page.AcademicYearId,
			Items = page.Items, TotalCount = page.TotalCount, PageNumber = page.PageNumber,
			PageSize = page.PageSize, Summary = summary
		};
	}

	private static MobileChapterSummaryDto Summarize(List<MobileChapterDto> rows) => new(
		rows.Count, rows.Count(c => c.ProgressPercentage == 100),
		rows.Count(c => c.ProgressPercentage is > 0 and < 100),
		rows.Count(c => c.ProgressPercentage == 0),
		rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(c => c.ProgressPercentage)));

	public async Task<List<LearningResource>> ReadResourcesAsync(MobilePlacement p, Guid subjectId, Guid? chapterId = null)
	{
		var allowed = (await ReadChaptersAsync(p)).Where(x => x.SubjectId == subjectId && (!chapterId.HasValue || x.Id == chapterId)).Select(x => x.Id).ToArray();
		return await resources.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StandardId == p.StandardId && x.SubjectId == subjectId &&
			Enumerable.Contains(allowed, x.ChapterId) && (!x.DivisionId.HasValue || x.DivisionId == p.DivisionId) &&
			x.Status == LearningResourceStatus.Published && (x.Visibility == LearningResourceVisibility.Students || x.Visibility == LearningResourceVisibility.Public));
	}

	public async Task<MobileChapterDto> GetChapterAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var chapter = (await ReadChaptersAsync(p)).SingleOrDefault(x => x.Id == id) ?? throw new EntityNotFoundException(typeof(AssignedSubjectChapter), id);
		chapter.Resources = (await ReadResourcesAsync(p, chapter.SubjectId, id)).Select(r => new MobileAttachmentDto(r.Id, r.Title,
			$"/api/mobile/v1/academics/syllabus/chapters/{id}/resources/{r.Id}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}", r.ContentType, r.Size, r.FileName)).ToList();
		foreach (var doc in await ReadDocumentsAsync(p, id)) {
			var name = System.IO.Path.GetFileName(doc.BlobName);
			chapter.Resources.Add(new(doc.Id, name,
				$"/api/mobile/v1/academics/syllabus/chapters/{id}/resources/{doc.Id}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}",
				DocumentContentType(name), 0, name));
		}
		return chapter;
	}
	private async Task<List<AssignedSubjectDocument>> ReadDocumentsAsync(MobilePlacement p, Guid chapterId)
	{
		var allowed = await context.GetSubjectsAsync(p);
		var ids = allowed.Select(x => x.Id).ToArray();
		var chapter = await chapters.GetAsync(chapterId);
		return await documents.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ChapterId == chapterId &&
			x.AssignedSubjectId == chapter.AssignedSubjectId && Enumerable.Contains(ids, x.AssignedSubjectId));
	}
	private static string DocumentContentType(string name) => System.IO.Path.GetExtension(name).ToLowerInvariant() switch {
		".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
		".doc" => "application/msword", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", _ => "application/octet-stream"
	};

	public async Task<MobileFileDto> DownloadAsync(Guid chapterId, Guid resourceId, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var chapter = (await ReadChaptersAsync(p)).SingleOrDefault(x => x.Id == chapterId) ?? throw new EntityNotFoundException(typeof(AssignedSubjectChapter), chapterId);
		var resource = (await ReadResourcesAsync(p, chapter.SubjectId, chapterId)).SingleOrDefault(x => x.Id == resourceId);
		if (resource != null) return await files.DownloadPublishedAsync(resource.BlobName, resource.FileName, resource.ContentType);
		var document = (await ReadDocumentsAsync(p, chapterId)).SingleOrDefault(x => x.Id == resourceId)
			?? throw new EntityNotFoundException(typeof(AssignedSubjectDocument), resourceId);
		var name = System.IO.Path.GetFileName(document.BlobName);
		return await files.DownloadPublishedAsync(document.BlobName, name, DocumentContentType(name));
	}
}
