using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportCatalog(
	IAsyncQueryableExecuter asyncExecuter,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IRepository<Division, Guid> divisionRepository) : ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, AcademicDynamicDatasetDefinition> DatasetDefinitions =
		new Dictionary<string, AcademicDynamicDatasetDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[AcademicDynamicDatasets.CurriculumSubjects] = new(
				AcademicDynamicDatasets.CurriculumSubjects,
				AcademicReportModules.Curriculum,
				"Curriculum subjects",
				"One row per subject assigned to a class, including setup, teacher and chapter totals.",
				[
					new("class", "Class"), new("subject", "Subject"), new("subjectCode", "Subject code"),
					new("subjectType", "Subject type"), new("status", "Status"),
					new("weeklyPeriods", "Weekly periods"), new("difficultyLevel", "Difficulty level"),
					new("subjectGroup", "Subject group"), new("primaryTeacher", "Primary teacher"),
					new("secondaryTeachers", "Secondary teachers"), new("chapterCount", "Chapter count"),
					new("averageProgress", "Average progress"), new("setupHealthScore", "Setup health score"),
					new("learningGoals", "Learning goals"), new("prerequisites", "Prerequisites"),
					new("description", "Description")
				],
				["class", "subject", "subjectCode", "primaryTeacher", "chapterCount", "averageProgress"]),
			[AcademicDynamicDatasets.CurriculumChapters] = new(
				AcademicDynamicDatasets.CurriculumChapters,
				AcademicReportModules.Curriculum,
				"Curriculum chapters",
				"One row per curriculum chapter with schedule, progress, coverage and planning details.",
				[
					new("class", "Class"), new("subject", "Subject"), new("chapterNumber", "Chapter number"),
					new("chapterName", "Chapter"), new("unitName", "Unit"), new("termName", "Term"),
					new("topic", "Topic"), new("plannedStart", "Planned start"), new("plannedEnd", "Planned end"),
					new("estimatedTeachingDays", "Estimated teaching days"), new("estimatedPeriods", "Estimated periods"),
					new("revisionDays", "Revision days"), new("progressPercentage", "Progress"),
					new("coverageStatus", "Coverage status"), new("publishStatus", "Publish status"),
					new("reviewStatus", "Review status"), new("healthScore", "Health score"),
					new("marksWeightage", "Marks weightage"), new("difficultyLevel", "Difficulty level"),
					new("bloomLevel", "Bloom level"), new("description", "Description")
				],
				["class", "subject", "chapterNumber", "chapterName", "plannedStart", "plannedEnd", "progressPercentage"]),
			[AcademicDynamicDatasets.CurriculumTeachers] = new(
				AcademicDynamicDatasets.CurriculumTeachers,
				AcademicReportModules.Curriculum,
				"Curriculum subject teachers",
				"One row per teacher assigned to a class subject, including primary and secondary teacher roles.",
				[
					new("class", "Class"), new("subject", "Subject"), new("subjectCode", "Subject code"),
					new("teacherName", "Teacher"), new("teacherCode", "Teacher code"),
					new("teacherType", "Teacher type"), new("weeklyPeriods", "Weekly periods"),
					new("subjectStatus", "Subject status")
				],
				["class", "subject", "teacherName", "teacherType"])
		};

	public IReadOnlyList<AcademicDynamicDatasetDefinition> GetDatasets(string module) =>
		DatasetDefinitions.Values
			.Where(dataset => string.Equals(dataset.Module, module, StringComparison.OrdinalIgnoreCase))
			.OrderBy(dataset => dataset.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();

	public AcademicDynamicDatasetDefinition GetDataset(string datasetId) =>
		DatasetDefinitions.TryGetValue(datasetId, out var dataset)
			? dataset
			: throw AcademicDynamicReportGuard.DatasetNotAllowed(datasetId);

	public async Task<AcademicDynamicReportCatalogSnapshot> BuildSnapshotAsync(AcademicAiReportCatalogScope scope)
	{
		var query = await assignedSubjectRepository.WithDetailsAsync(
			subject => subject.Subject,
			subject => subject.Standard,
			subject => subject.Chapters);
		query = query.Where(item =>
			item.Standard.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.ClassIds is { Count: > 0 })
		{
			query = query.Where(item => scope.ClassIds.Contains(item.StandardId));
		}
		var assignments = await asyncExecuter.ToListAsync(query);

		var divisionQuery = await divisionRepository.GetQueryableAsync();
		divisionQuery = divisionQuery.Where(item =>
			item.Standard.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.DivisionIds is { Count: > 0 })
		{
			divisionQuery = divisionQuery.Where(item => scope.DivisionIds.Contains(item.Id));
		}
		var divisions = await asyncExecuter.ToListAsync(divisionQuery);

		var classes = assignments
			.Where(item => item.Standard != null)
			.GroupBy(item => item.StandardId)
			.Select(group => new CatalogClass(group.Key, group.First().Standard.Name))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => item with { Token = $"class_{index + 1}" })
			.ToList();
		var classTokens = classes.ToDictionary(item => item.Id, item => item.Token);

		var divisionItems = divisions
			.Where(item => classTokens.ContainsKey(item.StandardId))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => new CatalogDivision(
				$"division_{index + 1}", item.Id, item.Name, item.StandardId, classTokens[item.StandardId]))
			.ToList();

		var subjects = assignments
			.Where(item => item.Subject != null)
			.GroupBy(item => item.SubjectId)
			.Select(group => new CatalogSubject(group.Key, group.First().Subject.Name))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => item with { Token = $"subject_{index + 1}" })
			.ToList();
		var subjectTokens = subjects.ToDictionary(item => item.Id, item => item.Token);

		var chapters = assignments
			.SelectMany(assignment => assignment.Chapters.Select(chapter => new
			{
				chapter.Id,
				chapter.ChapterName,
				assignment.StandardId,
				assignment.SubjectId
			}))
			.OrderBy(item => item.ChapterName, StringComparer.OrdinalIgnoreCase)
			.Take(1000)
			.Select((item, index) => new CatalogChapter(
				$"chapter_{index + 1}", item.Id, item.ChapterName, item.StandardId, item.SubjectId,
				classTokens.GetValueOrDefault(item.StandardId, string.Empty),
				subjectTokens.GetValueOrDefault(item.SubjectId, string.Empty)))
			.ToList();

		return new AcademicDynamicReportCatalogSnapshot(classes, divisionItems, subjects, chapters);
	}
}

public sealed record AcademicDynamicDatasetDefinition(
	string Id,
	string Module,
	string Name,
	string Description,
	IReadOnlyList<AcademicDynamicColumnDefinition> Columns,
	IReadOnlyList<string> DefaultColumns);

public sealed record AcademicDynamicColumnDefinition(string Id, string Label);

public sealed record AcademicDynamicReportCatalogSnapshot(
	IReadOnlyList<CatalogClass> Classes,
	IReadOnlyList<CatalogDivision> Divisions,
	IReadOnlyList<CatalogSubject> Subjects,
	IReadOnlyList<CatalogChapter> Chapters);

public sealed record CatalogClass(Guid Id, string Name, string Token = "");
public sealed record CatalogDivision(string Token, Guid Id, string Name, Guid StandardId, string ClassToken);
public sealed record CatalogSubject(Guid Id, string Name, string Token = "");
public sealed record CatalogChapter(
	string Token,
	Guid Id,
	string Name,
	Guid StandardId,
	Guid SubjectId,
	string ClassToken,
	string SubjectToken);
