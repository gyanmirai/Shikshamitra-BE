using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

[Collection(ApiTestCollection.Name)]
public class ExamGradeOptionsApi_Tests : ShikshaMitraApiTestBase
{
    private const string Route = "/api/academics/exams/marks/grade-options";

    public ExamGradeOptionsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        Client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
    }

    [Fact]
    public async Task Grade_Options_Should_Require_Record_Marks_View()
    {
        using var deniedClient = Factory.CreateClient();
        deniedClient.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
        deniedClient.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
        var response = await deniedClient.GetAsync($"{Route}?ExamId={Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Grade_Options_Should_Include_Grades_After_Row_500_Without_Mark_Or_Student_Detail()
    {
        var fixture = await SeedGradeOptionsAsync();
        using var capture = new GradeOptionQueries();
        var response = await Client.GetAsync($"{Route}?ExamId={fixture.ExamId}&DivisionId={fixture.DivisionId}&StandardId={fixture.StandardId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var grades = await response.Content.ReadFromJsonAsync<List<string>>();
        grades.ShouldNotBeNull();
        grades.OrderBy(grade => grade, StringComparer.Ordinal).ShouldBe(new[] { "A*", "Z" });
        capture.Commands.ShouldNotBeEmpty();
        capture.Commands.ShouldAllBe(sql => !sql.Contains("\"Feedback\"", StringComparison.Ordinal) &&
            !sql.Contains("\"PrimaryContactNo\"", StringComparison.Ordinal));
        capture.Commands.Count(sql => sql.Contains($"\"{fixture.AssignmentTable}\"", StringComparison.Ordinal)).ShouldBeLessThanOrEqualTo(2);
        var empty = await Client.GetAsync($"{Route}?ExamId={fixture.ExamId}&SubjectId={fixture.SecondarySubjectId}&DivisionId={fixture.DivisionId}");
        empty.StatusCode.ShouldBe(HttpStatusCode.OK, await empty.Content.ReadAsStringAsync());
        (await empty.Content.ReadFromJsonAsync<List<string>>()).ShouldBeEmpty();
    }

    private async Task<GradeOptionFixture> SeedGradeOptionsAsync()
    {
        using var serviceScope = Factory.Services.CreateScope();
        var services = serviceScope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
        var standard = await db.Set<Standard>().SingleAsync(standard => standard.Id == Factory.SeededStandardId);
        var stage = await db.Set<EducationMediumStage>().SingleAsync(stage => stage.Id == standard.EducationMediumStageId);
        var medium = await db.Set<EducationMedium>().SingleAsync(medium => medium.Id == stage.EducationMediumId);
        var board = await db.Set<EducationBoard>().SingleAsync(board => board.Id == medium.EducationBoardId);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, $"Grade lookup {tag}", 600);
        var scale = new GradingScale(Guid.NewGuid(), tenantId, $"Grade lookup {tag}", "200", medium.Id);
        scale.GradeRanges.Add(new GradeRange(Guid.NewGuid(), scale.Id, 0, 19, "ForeignOnly"));
        scale.GradeRanges.Add(new GradeRange(Guid.NewGuid(), scale.Id, 20, 179, " Z "));
        scale.GradeRanges.Add(new GradeRange(Guid.NewGuid(), scale.Id, 180, 200, " A* "));
        var type = new ExamType(Guid.NewGuid(), tenantId, $"Grade lookup {tag}", null, medium.Id);
        var exam = new Exam(Guid.NewGuid(), type.Id, $"Grade lookup {tag}", standard.Id, tenantId);
        exam.AddDivision(Guid.NewGuid(), division.Id);
        var subjects = Enumerable.Range(1, 8).Select(index => new Subject(Guid.NewGuid(), tenantId,
            $"Grade lookup {tag} subject {index}", "#112233", medium.Id)).ToList();
        foreach (var subject in subjects)
            exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id, DateTime.Today, null, null, 100, 35, gradeScalingId: scale.Id));
        db.AddRange(division, scale, type, exam);
        db.AddRange(subjects);
        var students = Enumerable.Range(1, 501).Select(index =>
        {
            var student = new Student(Guid.NewGuid(), tenantId, $"Eligible {index}", null, "Lookup", board.AcademicYearId,
                board.Id, medium.Id, stage.Id, standard.Id, division.Id, rollNo: index, status: StudentLifeCycleStatusEnum.Admission);
            student.ApproveAdmission();
            return student;
        }).ToList();
        db.AddRange(students);
        db.AddRange(students.Select((student, index) => new ExamMark(Guid.NewGuid(), exam.Id, student.Id, subjects[0].Id,
            division.Id, index == 500 ? 95 : 40, false, "Not returned", tenantId)));
        var unapproved = new Student(Guid.NewGuid(), tenantId, "Unapproved", null, "Lookup", board.AcademicYearId,
            board.Id, medium.Id, stage.Id, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Admission);
        db.Add(unapproved);
        db.Add(new ExamMark(Guid.NewGuid(), exam.Id, unapproved.Id, subjects[0].Id, division.Id, 5, false, tenantId: tenantId));
        await db.SaveChangesAsync();
        var foreignTenantId = Guid.NewGuid();
        using (services.GetRequiredService<ICurrentTenant>().Change(foreignTenantId))
        {
            db.Add(new ExamMark(Guid.NewGuid(), exam.Id, students[0].Id, subjects[1].Id, division.Id, 5, false, tenantId: foreignTenantId));
            await db.SaveChangesAsync();
        }
        var assignmentTable = db.Model.FindEntityType(typeof(AssignedSubject))!.GetTableName()!;
        await uow.CompleteAsync();
        return new(exam.Id, subjects[1].Id, division.Id, standard.Id, assignmentTable);
    }

    private sealed record GradeOptionFixture(Guid ExamId, Guid SecondarySubjectId, Guid DivisionId, Guid StandardId, string AssignmentTable);

    private sealed class GradeOptionQueries : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();
        private readonly ConcurrentQueue<string> _commands = new();
        private readonly IDisposable _listener;
        public GradeOptionQueries() => _listener = DiagnosticListener.AllListeners.Subscribe(this);
        public IReadOnlyCollection<string> Commands => _commands.ToArray();
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
                _subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
        }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandExecutedEventData command) _commands.Enqueue(command.Command.CommandText);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _listener.Dispose();
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }
}
