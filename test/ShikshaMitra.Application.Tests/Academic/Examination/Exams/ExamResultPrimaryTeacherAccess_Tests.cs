using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
    [Fact]
    public async Task Result_Preview_Should_Allow_Primary_Teacher_With_Unrelated_ClassTeacher_Scope()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreatePrimaryTeacherResultFixtureAsync();

                using (_currentPrincipalAccessor.Change(BuildTeacherPrincipal(fixture.UserId)))
                {
                    var reportService = GetRequiredService<IAcademicReportAppService>();
                    var preview = await reportService.PreviewAsync(new AcademicReportRequestDto
                    {
                        ReportType = AcademicReportTypes.ResultWithAllSubjectList,
                        Page = 1,
                        PageSize = 50,
                        Filters = new AcademicReportFilterDto
                        {
                            ExamId = fixture.Exam.Id,
                            ClassIds = [fixture.Exam.StandardId],
                            DivisionIds = [fixture.ExamDivisionId]
                        }
                    });

                    preview.NormalizedFilters.ClassIds.ShouldBe([fixture.Exam.StandardId]);
                    preview.NormalizedFilters.DivisionIds.ShouldBe([fixture.ExamDivisionId]);

                    var resultService = GetRequiredService<IExamResultAppService>();
                    await Should.NotThrowAsync(() =>
                        resultService.GetReadinessAsync(fixture.Exam.Id, fixture.ExamDivisionId));
                }
            }
        });
    }

    [Fact]
    public async Task Result_Preview_Should_Not_Extend_Primary_Teacher_Access_Outside_Exam_Divisions()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreatePrimaryTeacherResultFixtureAsync();

                using (_currentPrincipalAccessor.Change(BuildTeacherPrincipal(fixture.UserId)))
                {
                    var reportService = GetRequiredService<IAcademicReportAppService>();
                    var exception = await Should.ThrowAsync<BusinessException>(() =>
                        reportService.PreviewAsync(new AcademicReportRequestDto
                        {
                            ReportType = AcademicReportTypes.ResultWithAllSubjectList,
                            Page = 1,
                            PageSize = 50,
                            Filters = new AcademicReportFilterDto
                            {
                                ExamId = fixture.Exam.Id,
                                ClassIds = [fixture.Exam.StandardId],
                                DivisionIds = [fixture.UnrelatedDivisionId]
                            }
                        }));

                    exception.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
                }
            }
        });
    }

    [Fact]
    public async Task Result_Preview_Should_Deny_Teacher_Not_Assigned_To_Every_Exam_Subject()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreatePrimaryTeacherResultFixtureAsync(includeUnassignedSubject: true);

                using (_currentPrincipalAccessor.Change(BuildTeacherPrincipal(fixture.UserId)))
                {
                    var reportService = GetRequiredService<IAcademicReportAppService>();
                    var exception = await Should.ThrowAsync<BusinessException>(() =>
                        reportService.PreviewAsync(new AcademicReportRequestDto
                        {
                            ReportType = AcademicReportTypes.ResultWithAllSubjectList,
                            Page = 1,
                            PageSize = 50,
                            Filters = new AcademicReportFilterDto
                            {
                                ExamId = fixture.Exam.Id,
                                ClassIds = [fixture.Exam.StandardId],
                                DivisionIds = [fixture.ExamDivisionId]
                            }
                        }));

                    exception.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);

                    var resultService = GetRequiredService<IExamResultAppService>();
                    var readinessException = await Should.ThrowAsync<BusinessException>(() =>
                        resultService.GetReadinessAsync(fixture.Exam.Id, fixture.ExamDivisionId));
                    readinessException.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
                }
            }
        });
    }

    private async Task<PrimaryTeacherResultFixture> CreatePrimaryTeacherResultFixtureAsync(
        bool includeUnassignedSubject = false)
    {
        var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
        var stage = await _stageRepository.GetAsync(stageId);
        var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
        var board = await _boardRepository.GetAsync(medium.EducationBoardId);
        var unique = Guid.NewGuid().ToString("N");
        var teacherUserId = Guid.NewGuid();
        var teacher = await CreateTeacherAsync(teacherUserId, unique);

        var examStandard = await _standardManager.CreateAsync(
            stageId,
            $"Result Std {unique}"[..30],
            10,
            shiftId: null);
        await _standardRepository.InsertAsync(examStandard, autoSave: true);
        var examDivision = await _divisionManager.CreateAsync(
            examStandard.Id,
            $"A-{unique}"[..30],
            35);
        await _divisionRepository.InsertAsync(examDivision, autoSave: true);

        var assignedSubject = await CreatePrimaryTeacherSubjectAsync(
            examStandard.Id,
            stage.EducationMediumId,
            teacher.Id,
            $"Hindi {unique}"[..30]);
        var examType = await _examTypeManager.CreateAsync(
            $"Oral {unique}"[..30],
            description: null,
            stage.EducationMediumId);
        var examSubjectIds = new List<Guid> { assignedSubject.SubjectId };
        if (includeUnassignedSubject)
        {
            var unassignedSubject = await CreateSubjectAsync(
                stage.EducationMediumId,
                $"English {unique}"[..30]);
            examSubjectIds.Add(unassignedSubject.Id);
        }

        var exam = _examManager.Create(
            examType.Id,
            $"Hindi oral {unique}"[..40],
            examStandard.Id,
            [examDivision.Id],
            examSubjectIds);
        await _examRepository.InsertAsync(exam, autoSave: true);

        var unrelatedStandard = await _standardManager.CreateAsync(
            stageId,
            $"Other Std {unique}"[..30],
            11,
            shiftId: null);
        await _standardRepository.InsertAsync(unrelatedStandard, autoSave: true);
        var unrelatedDivision = await _divisionManager.CreateAsync(
            unrelatedStandard.Id,
            $"B-{unique}"[..30],
            35);
        await _divisionRepository.InsertAsync(unrelatedDivision, autoSave: true);

        var classTeacherRepository = GetRequiredService<
            IRepository<ShikshaMitra.Academic.ClassTeacher.ClassTeacher, Guid>>();
        await classTeacherRepository.InsertAsync(
            new ShikshaMitra.Academic.ClassTeacher.ClassTeacher(
                Guid.NewGuid(),
                TestTenantId,
                unrelatedStandard.Id,
                unrelatedDivision.Id,
                teacher.Id,
                stage.EducationMediumId,
                board.AcademicYearId,
                board.Id,
                medium.Id,
                stage.Id),
            autoSave: true);
        await _scopeRepository.InsertAsync(
            new UserScopeAssignment(
                Guid.NewGuid(),
                TestTenantId,
                userId: teacherUserId,
                academicYearId: board.AcademicYearId,
                educationBoardId: board.Id,
                educationMediumId: medium.Id,
                educationMediumStageId: stage.Id,
                standardId: unrelatedStandard.Id,
                divisionId: unrelatedDivision.Id,
                description: "Unrelated class-teacher scope regression fixture"),
            autoSave: true);
        await _userScopeService.InvalidateUserScopeCacheAsync(teacherUserId);

        return new PrimaryTeacherResultFixture(
            teacherUserId,
            exam,
            examDivision.Id,
            unrelatedDivision.Id);
    }

    private sealed record PrimaryTeacherResultFixture(
        Guid UserId,
        Exam Exam,
        Guid ExamDivisionId,
        Guid UnrelatedDivisionId);
}
