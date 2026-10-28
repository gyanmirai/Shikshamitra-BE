using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectCreationReadPerformance_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Bulk_Create_Should_Share_Validation_And_Avoid_New_Subject_Usage_Reads()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
            {
                var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
                var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
                var service = GetRequiredService<ISubjectAppService>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                int singleReads;
                using (var capture = new AcademicQueryCounter(db.Database.GetDbConnection()))
                {
                    await service.BulkCreateAsync(new BulkCreateSubjectsDto { Subjects = [Input("Single", stage.EducationMediumId)] });
                    singleReads = capture.ReadCount;
                }
                using (var capture = new AcademicQueryCounter(db.Database.GetDbConnection()))
                {
                    var rows = await service.BulkCreateAsync(new BulkCreateSubjectsDto
                    {
                        Subjects = Enumerable.Range(0, 6).Select(index => Input($"Batch {index}", stage.EducationMediumId)).ToList()
                    });
                    rows.Count.ShouldBe(6);
                    rows.ShouldAllBe(row => !row.IsUsed && row.CanDelete && row.UsedInClassesCount == 0);
                    capture.ReadCount.ShouldBeLessThanOrEqualTo(singleReads + 2);
                }
                await db.SaveChangesAsync();
                (await GetRequiredService<IRepository<SubjectAuditLog, Guid>>().CountAsync()).ShouldBe(7);
                var duplicate = await Should.ThrowAsync<BusinessException>(() => service.BulkCreateAsync(new BulkCreateSubjectsDto
                {
                    Subjects = [Input("batch 0", stage.EducationMediumId)]
                }));
                duplicate.Code.ShouldBe(CurriculumErrorCodes.DuplicateSubjectName);
            }
        });
    }

    [Fact]
    public async Task Subject_Lookup_Should_Preserve_Filter_Order_And_Avoid_Operational_Usage_Reads()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
                var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
                var repository = GetRequiredService<IRepository<Subject, Guid>>();
                await repository.InsertAsync(new Subject(Guid.NewGuid(), tenantId, "Zulu", "#00A76F", stage.EducationMediumId, status: SubjectMasterStatus.Active, sortOrder: 2), autoSave: true);
                await repository.InsertAsync(new Subject(Guid.NewGuid(), tenantId, "Alpha", "#00A76F", stage.EducationMediumId, status: SubjectMasterStatus.Inactive, sortOrder: 1), autoSave: true);
                await repository.InsertAsync(new Subject(Guid.NewGuid(), tenantId, "Archived", "#00A76F", stage.EducationMediumId, status: SubjectMasterStatus.Archived), autoSave: true);
                var service = GetRequiredService<ISubjectAppService>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
                var rows = await service.GetLookupAsync(stage.EducationMediumId);
                rows.Select(row => row.Name).ShouldBe(new[] { "Alpha", "Zulu" });
                capture.ReadCommands.Count(sql => !sql.Contains("AbpTenant", StringComparison.Ordinal))
                    .ShouldBe(1, string.Join(Environment.NewLine, capture.ReadCommands));
                db.ChangeTracker.Entries<Subject>().ShouldBeEmpty();
            }
        });
    }

    private static CreateUpdateSubjectDto Input(string name, Guid mediumId) => new()
    {
        Name = name, Color = "#00A76F", EducationMediumId = mediumId
    };
}
