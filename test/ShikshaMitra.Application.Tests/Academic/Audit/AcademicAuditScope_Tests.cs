using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Audit;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Audit;

public class AcademicAuditScope_Tests
    : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Subject_Audits_Should_Stay_Within_The_Selected_Year_And_Medium()
    {
        var years = GetRequiredService<IRepository<AcademicYear, Guid>>();
        var boards = GetRequiredService<IRepository<EducationBoard, Guid>>();
        var mediums = GetRequiredService<IRepository<EducationMedium, Guid>>();
        var subjects = GetRequiredService<IRepository<Subject, Guid>>();
        var logs = GetRequiredService<IRepository<SubjectAuditLog, Guid>>();
        var calendars = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
        var calendarLogs = GetRequiredService<IRepository<AcademicCalendarAuditLog, Guid>>();
        var groups = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
        var auditService = GetRequiredService<IAcademicAuditTrailAppService>();
        var marker = $"AuditScope{Guid.NewGuid():N}";
        var firstYearId = Guid.NewGuid();
        var secondYearId = Guid.NewGuid();
        var gsebMediumId = Guid.NewGuid();
        var cbseMediumId = Guid.NewGuid();
        var nextYearMediumId = Guid.NewGuid();

        await years.InsertAsync(new AcademicYear(firstYearId, null, $"{marker} Year 1",
            new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), autoSave: true);
        await years.InsertAsync(new AcademicYear(secondYearId, null, $"{marker} Year 2",
            new DateTime(2027, 4, 1), new DateTime(2028, 3, 31)), autoSave: true);
        await InsertBoardAndMediumAsync(boards, mediums, firstYearId, gsebMediumId,
            EducationBoardType.GSEB, "Gujarati");
        await InsertBoardAndMediumAsync(boards, mediums, firstYearId, cbseMediumId,
            EducationBoardType.CBSE, "English");
        await InsertBoardAndMediumAsync(boards, mediums, secondYearId, nextYearMediumId,
            EducationBoardType.CBSE, "English");

        var gsebLogId = await InsertSubjectAuditAsync(subjects, logs, gsebMediumId, marker);
        var cbseLogId = await InsertSubjectAuditAsync(subjects, logs, cbseMediumId, marker);
        var nextYearLogId = await InsertSubjectAuditAsync(subjects, logs, nextYearMediumId, marker);
        var gsebCalendarLogId = await InsertCalendarAuditAsync(calendars, calendarLogs,
            gsebMediumId, firstYearId, marker);
        var cbseCalendarLogId = await InsertCalendarAuditAsync(calendars, calendarLogs,
            cbseMediumId, firstYearId, marker);
        var gsebGroupId = Guid.NewGuid();
        var cbseGroupId = Guid.NewGuid();
        await groups.InsertAsync(new ElectiveGroup(gsebGroupId, null, $"{marker} Group", "#00A76F",
            gsebMediumId), autoSave: true);
        await groups.InsertAsync(new ElectiveGroup(cbseGroupId, null, $"{marker} Group", "#00A76F",
            cbseMediumId), autoSave: true);

        var gseb = await QueryAsync(auditService, firstYearId, gsebMediumId, marker);
        gseb.Items.Select(x => x.SourceAuditLogId).ShouldBe(new Guid?[] { gsebLogId });

        var cbse = await QueryAsync(auditService, firstYearId, cbseMediumId, marker);
        cbse.Items.Select(x => x.SourceAuditLogId).ShouldBe(new Guid?[] { cbseLogId });

        var nextYear = await QueryAsync(auditService, secondYearId, nextYearMediumId, marker);
        nextYear.Items.Select(x => x.SourceAuditLogId).ShouldBe(new Guid?[] { nextYearLogId });

        var mismatched = await QueryAsync(auditService, secondYearId, gsebMediumId, marker);
        mismatched.TotalCount.ShouldBe(0);

        var yearOnly = await QueryAsync(auditService, firstYearId, null, marker);
        yearOnly.Items.Select(x => x.SourceAuditLogId).ShouldBe(new Guid?[] { gsebLogId, cbseLogId }, ignoreOrder: true);

        var allGsebEvents = await auditService.GetListAsync(new AcademicAuditEventListRequestDto
        {
            AcademicYearId = firstYearId,
            AcademicMediumId = gsebMediumId,
            Search = marker,
            MaxResultCount = 100
        });
        allGsebEvents.Items.ShouldContain(x => x.SourceAuditLogId == gsebCalendarLogId);
        allGsebEvents.Items.ShouldContain(x => x.EntityId == gsebGroupId);
        allGsebEvents.Items.ShouldNotContain(x => x.SourceAuditLogId == cbseCalendarLogId);
        allGsebEvents.Items.ShouldNotContain(x => x.EntityId == cbseGroupId);
    }

    private static async Task InsertBoardAndMediumAsync(
        IRepository<EducationBoard, Guid> boards,
        IRepository<EducationMedium, Guid> mediums,
        Guid yearId,
        Guid mediumId,
        EducationBoardType boardType,
        string mediumName)
    {
        var boardId = Guid.NewGuid();
        await boards.InsertAsync(new EducationBoard(boardId, null, boardType, yearId), autoSave: true);
        await mediums.InsertAsync(new EducationMedium(mediumId, null, mediumName, boardId), autoSave: true);
    }

    private static async Task<Guid> InsertSubjectAuditAsync(
        IRepository<Subject, Guid> subjects,
        IRepository<SubjectAuditLog, Guid> logs,
        Guid mediumId,
        string marker)
    {
        var subjectId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var name = $"{marker} Subject";
        await subjects.InsertAsync(new Subject(subjectId, null, name, "#00A76F", mediumId), autoSave: true);
        await logs.InsertAsync(new SubjectAuditLog(logId, null, subjectId, "Created", "{}", "{}",
            Guid.NewGuid(), entityName: name), autoSave: true);
        return logId;
    }

    private static async Task<Guid> InsertCalendarAuditAsync(
        IRepository<AcademicCalendar, Guid> calendars,
        IRepository<AcademicCalendarAuditLog, Guid> logs,
        Guid mediumId,
        Guid yearId,
        string marker)
    {
        var calendarId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var name = $"{marker} Calendar";
        await calendars.InsertAsync(new AcademicCalendar(calendarId, null, mediumId, yearId, name,
            new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), autoSave: true);
        await logs.InsertAsync(new AcademicCalendarAuditLog(logId, null, calendarId,
            CalendarAuditAction.CalendarCreated, "{}", "{}", Guid.NewGuid(), entityName: name),
            autoSave: true);
        return logId;
    }

    private static Task<Volo.Abp.Application.Dtos.PagedResultDto<AcademicAuditEventDto>> QueryAsync(
        IAcademicAuditTrailAppService service,
        Guid yearId,
        Guid? mediumId,
        string marker) =>
        service.GetListAsync(new AcademicAuditEventListRequestDto
        {
            AcademicYearId = yearId,
            AcademicMediumId = mediumId,
            SubModuleCode = "subject_creation",
            Search = marker,
            MaxResultCount = 100
        });
}
