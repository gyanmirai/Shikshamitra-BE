using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Audit;

public partial class AcademicAuditScope_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_Audit_Source_Should_Preserve_Complete_History_And_Skip_Unrelated_Sources(bool searchOldRecord)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var yearId = Guid.NewGuid(); var mediumId = Guid.NewGuid(); var subjectId = Guid.NewGuid();
            await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(yearId, null,
                $"Audit history {yearId:N}", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await InsertBoardAndMediumAsync(GetRequiredService<IRepository<EducationBoard, Guid>>(),
                GetRequiredService<IRepository<EducationMedium, Guid>>(), yearId, mediumId, EducationBoardType.CBSE, "Audit history medium");
            await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(subjectId, null,
                "Audit history subject", "#00A76F", mediumId), autoSave: true);
            var logs = Enumerable.Range(0, 501).Select(index =>
            {
                var log = new SubjectAuditLog(Guid.NewGuid(), null, subjectId, "Created", "{}", "{}", Guid.Empty,
                    entityName: index == 500 ? "OlderMatch" : $"Audit subject {index}");
                typeof(SubjectAuditLog).GetProperty(nameof(SubjectAuditLog.ChangedAt))!.SetValue(log, new DateTime(2026, 10, 20).AddMinutes(-index));
                return log;
            }).ToList();
            await GetRequiredService<IRepository<SubjectAuditLog, Guid>>().InsertManyAsync(logs, autoSave: true);
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            db.ChangeTracker.Clear();
            using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
            using var rowsRead = new TimetableReadCapture(db.Database.GetDbConnection());
            var page = await GetRequiredService<IAcademicAuditTrailAppService>().GetListAsync(new AcademicAuditEventListRequestDto
            {
                AcademicYearId = yearId, AcademicMediumId = mediumId, SubModuleCode = "subject_creation",
                Search = searchOldRecord ? "OlderMatch" : null, MaxResultCount = 10
            });
            page.TotalCount.ShouldBe(searchOldRecord ? 1 : 501);
            page.Items.Count.ShouldBe(searchOldRecord ? 1 : 10);
            if (searchOldRecord) page.Items.Single().SourceAuditLogId.ShouldBe(logs[500].Id);
            capture.ReadCommands.ShouldAllBe(sql => !sql.Contains("CalendarAuditLogs", StringComparison.Ordinal) &&
                !sql.Contains("RoomAuditLogs", StringComparison.Ordinal) && !sql.Contains("SubstitutionAuditLogs", StringComparison.Ordinal) &&
                !sql.Contains("SeatingPlanAuditLogs", StringComparison.Ordinal) && !sql.Contains("ResultAuditLogs", StringComparison.Ordinal) &&
                !sql.Contains("RoomReservations", StringComparison.Ordinal) && !sql.Contains("SubstitutionRequests", StringComparison.Ordinal));
            db.ChangeTracker.Entries<SubjectAuditLog>().Count().ShouldBeLessThanOrEqualTo(10);
            if (!searchOldRecord) rowsRead.ReadsFor("SubjectAuditLogs").ShouldBeLessThanOrEqualTo(32);
        });
    }
}
