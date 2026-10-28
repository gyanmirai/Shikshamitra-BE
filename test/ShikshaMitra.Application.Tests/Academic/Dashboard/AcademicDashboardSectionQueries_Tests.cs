using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicDashboardSectionQueries_Tests : LessonPlanProgressTestBase
{
	[Fact]
	public async Task Sections_Should_Not_Count_Another_Tenants_Chapter_Progress()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (AsLessonPlanOwner())
			{
				using (Tenant.Change(TenantId))
				{
					await SeedContextAsync();
					await GetRequiredService<IRepository<ChapterProgress, Guid>>().InsertAsync(
						new ChapterProgress(ChapterId, DivisionId) { Progress = 50 }, autoSave: true);
				}
				using (Tenant.Change(Guid.NewGuid()))
				{
					var service = GetRequiredService<IAcademicDashboardAppService>();
					var teacher = await service.GetTeacherAssessmentAsync();
					var leadership = await service.GetPrincipalOperationsAsync();
					teacher.ReadinessItems.Single(item => item.Key == "syllabus-progress").CompletedCount.ShouldBe(0);
					leadership.ReadinessItems.Single(item => item.Key == "syllabus-progress").CompletedCount.ShouldBe(0);
				}
			}
		});
	}

	[Fact]
	public async Task Empty_School_Sections_Should_Preserve_Missing_Readiness_And_Health()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(Guid.NewGuid()))
			using (AsLessonPlanOwner())
			{
				var service = GetRequiredService<IAcademicDashboardAppService>();
				var teacher = await service.GetTeacherAssessmentAsync();
				var leadership = await service.GetPrincipalOperationsAsync();
				teacher.PendingEvaluations.ShouldBe(0);
				teacher.EvaluationsRate.ShouldBe(0);
				teacher.SystemAlertsCount.ShouldBe(0);
				teacher.ReadinessItems.Count.ShouldBe(13);
				teacher.ReadinessItems.ShouldAllBe(item => item.CompletedCount == 0 && item.TotalCount == 1);
				leadership.ReadinessRate.ShouldBe(0);
				leadership.EvaluationLag.ShouldBe("No pending evaluations");
				leadership.ActionCenterItems.ShouldBeEmpty();
				JsonSerializer.Serialize(teacher.DataHealthItems).ShouldBe(JsonSerializer.Serialize(leadership.DataHealthItems));
			}
		});
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Sections_Should_Keep_Read_Volume_Bounded_As_Unrelated_Curriculum_Grows(bool teacher)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				var assignedRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
				var teacherAssignment = await assignedRepository.GetAsync(item => item.SubjectId == SubjectId);
				teacherAssignment.SetPrimaryTeacher(TeacherEmployeeId);
				await assignedRepository.UpdateAsync(teacherAssignment, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var service = GetRequiredService<IAcademicDashboardAppService>();
				async Task ReadSectionAsync()
				{
					if (teacher) await service.GetTeacherAssessmentAsync();
					else await service.GetPrincipalOperationsAsync();
				}
				db.ChangeTracker.Clear();
				int smallRows;
				using (var capture = new SectionReads(db.Database.GetDbConnection()))
				{
					await ReadSectionAsync();
					smallRows = capture.Rows;
					smallRows.ShouldBeGreaterThan(0, "The diagnostic observer must capture actual query reads.");
				}
				for (var index = 0; index < 40; index++)
				{
					var subject = new Subject(Guid.NewGuid(), TenantId, $"Unrelated {index}", "#112233", AcademicMediumId);
					await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
					var assigned = new AssignedSubject(Guid.NewGuid(), TenantId, StandardId, subject.Id, $"U{index}");
					await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assigned, autoSave: true);
					var chapter = new AssignedSubjectChapter(Guid.NewGuid(), assigned.Id, 1, $"Unrelated chapter {index}",
						DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10))
					{ TenantId = TenantId, AssignedSubjectId = assigned.Id };
					await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(chapter, autoSave: true);
					await GetRequiredService<IRepository<ChapterProgress, Guid>>().InsertAsync(
						new ChapterProgress(chapter.Id, DivisionId) { Progress = 25 }, autoSave: true);
				}
				db.ChangeTracker.Clear();
				using var large = new SectionReads(db.Database.GetDbConnection());
				await ReadSectionAsync();
				large.Rows.ShouldBeLessThanOrEqualTo(smallRows + 5,
					"Section reads must aggregate school cohorts in SQL instead of returning one row per entity.");
				db.ChangeTracker.Entries<Subject>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
				db.ChangeTracker.Entries<ChapterProgress>().ShouldBeEmpty();
			}
		});
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(true, false)]
	[InlineData(false, false)]
	public async Task Teacher_Section_Should_Match_Legacy_With_All_Submission_States_And_Missing_Associations(
		bool hasEmployee, bool hasAssignment)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				if (hasEmployee) await SeedLessonPlanOwnerAsync();
				if (hasAssignment)
				{
					var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
					var assignment = await repository.GetAsync(item => item.SubjectId == SubjectId);
					assignment.SetPrimaryTeacher(TeacherEmployeeId);
					await repository.UpdateAsync(assignment, autoSave: true);
				}
				var standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
				var firstStandard = await standardRepository.GetAsync(StandardId);
				var secondStandard = await GetRequiredService<StandardManager>().CreateAsync(
					firstStandard.EducationMediumStageId, "Second dashboard class", 13, null);
				await standardRepository.InsertAsync(secondStandard, autoSave: true);
				var secondDivision = await GetRequiredService<DivisionManager>().CreateAsync(secondStandard.Id, "A", 30);
				await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(secondDivision, autoSave: true);
				var secondSubject = new Subject(Guid.NewGuid(), TenantId, "Science dashboard", "#112233", AcademicMediumId);
				await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(secondSubject, autoSave: true);
				var secondAssignment = new AssignedSubject(Guid.NewGuid(), TenantId, secondStandard.Id, secondSubject.Id, "SCI");
				if (hasAssignment) secondAssignment.SetPrimaryTeacher(TeacherEmployeeId);
				await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(secondAssignment, autoSave: true);
				foreach (var standardId in new[] { StandardId, secondStandard.Id })
				{
					foreach (var status in new[] { ExamStatus.Pending, ExamStatus.Published })
					{
						var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), $"Dashboard {status}", standardId, TenantId);
						exam.ChangeStatus(status);
						await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
					}
				}
				var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId,
					"Section status fixture", defaultSubmissionType: SubmissionType.OnlineText);
				var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId,
					"Section fixture", "All states", type.Id, SubmissionType.OnlineText,
					tenantId: TenantId, educationMediumId: AcademicMediumId);
				await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
				foreach (var status in Enum.GetValues<SubmissionStatus>())
				{
					await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(
						new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, Guid.NewGuid(), DivisionId,
							DateTime.UtcNow.AddDays(1), TenantId) { Status = status }, autoSave: true);
				}
				await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(
					new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, Guid.NewGuid(), DivisionId,
						DateTime.UtcNow.AddDays(1), TenantId)
					{ HomeworkAssignmentId = null, Status = SubmissionStatus.Reviewed }, autoSave: true);
				await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(
					new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, Guid.NewGuid(), Guid.NewGuid(),
						DateTime.UtcNow.AddDays(1), TenantId) { Status = SubmissionStatus.Submitted }, autoSave: true);
				var plan = await InsertPlanAsync();
				plan.Submit();
				await Plans.UpdateAsync(plan, autoSave: true);
				var previousYear = Input();
				previousYear.AcademicYearId = Guid.NewGuid();
				var historical = await InsertPlanAsync(previousYear);
				historical.Submit();
				await Plans.UpdateAsync(historical, autoSave: true);
				var otherTeacher = Input();
				otherTeacher.TeacherId = Guid.NewGuid();
				var unrelated = await InsertPlanAsync(otherTeacher);
				unrelated.Submit();
				await Plans.UpdateAsync(unrelated, autoSave: true);
				var service = GetRequiredService<IAcademicDashboardAppService>();
				var legacy = await service.GetTeacherOverviewAsync();
				var section = await service.GetTeacherAssessmentAsync();
				section.PendingEvaluations.ShouldBe(hasAssignment ? 3 : 0);
				section.UpcomingExams.ShouldBe(hasAssignment ? 2 : 0);
				section.EvaluationsRate.ShouldBe(hasAssignment ? 25 : 0);
				section.HomeworkStatus.Completed.ShouldBe(hasAssignment ? 2 : 0);
				section.HomeworkStatus.Pending.ShouldBe(hasAssignment ? 2 : 0);
				section.PendingEvaluations.ShouldBe(legacy.PendingEvaluations);
				section.ActionCenterItems.Single(item => item.Id == "lesson-review").Count.ShouldBe(hasEmployee ? 2 : 3);
				JsonSerializer.Serialize(section.ReadinessItems).ShouldBe(JsonSerializer.Serialize(legacy.ReadinessItems));
				JsonSerializer.Serialize(section.ActionCenterItems).ShouldBe(JsonSerializer.Serialize(legacy.ActionCenterItems));
				JsonSerializer.Serialize(section.DataHealthItems).ShouldBe(JsonSerializer.Serialize(legacy.DataHealthItems));
				JsonSerializer.Serialize(section.PendingTasks).ShouldBe(JsonSerializer.Serialize(legacy.PendingTasks));
				var leadership = await service.GetPrincipalOverviewAsync();
				var operations = await service.GetPrincipalOperationsAsync();
				operations.ReadinessRate.ShouldBe(leadership.ReadinessRate);
				operations.UpcomingExams.ShouldBe(2);
				JsonSerializer.Serialize(operations.ReadinessItems).ShouldBe(JsonSerializer.Serialize(leadership.ReadinessItems));
				JsonSerializer.Serialize(operations.ActionCenterItems).ShouldBe(JsonSerializer.Serialize(leadership.ActionCenterItems));
				JsonSerializer.Serialize(operations.DataHealthItems).ShouldBe(JsonSerializer.Serialize(leadership.DataHealthItems));
			}
		});
	}

	private sealed class SectionReads : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
	{
		private readonly DbConnection _connection;
		private readonly IDisposable _all;
		private readonly ConcurrentBag<IDisposable> _subscriptions = [];
		private int _rows;
		public int Rows => Volatile.Read(ref _rows);
		public SectionReads(DbConnection connection)
		{
			_connection = connection;
			_all = DiagnosticListener.AllListeners.Subscribe(this);
		}
		public void OnNext(DiagnosticListener listener)
		{
			if (listener.Name == "Microsoft.EntityFrameworkCore")
				_subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.DataReaderDisposing.Name));
		}
		public void OnNext(KeyValuePair<string, object?> value)
		{
			if (value.Value is DataReaderDisposingEventData data && data.Context != null &&
				ReferenceEquals(data.Context.Database.GetDbConnection(), _connection))
				Interlocked.Add(ref _rows, data.ReadCount);
		}
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void Dispose()
		{
			_all.Dispose();
			foreach (var subscription in _subscriptions) subscription.Dispose();
		}
	}
}
