using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
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
    [InlineData("asc", false)]
    [InlineData("desc", false)]
    [InlineData("asc", true)]
    [InlineData("desc", true)]
    public async Task Synthetic_Audit_Keys_Should_Keep_Actor_Date_And_Timestamp_Tie_Parity(string direction, bool filterActor)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var yearId = Guid.NewGuid();
            var mediumId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var otherActorId = Guid.NewGuid();
            var marker = $"Synthetic parity {Guid.NewGuid():N}";
            var createdAt = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
            await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(yearId, null,
                marker, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await InsertBoardAndMediumAsync(GetRequiredService<IRepository<EducationBoard, Guid>>(),
                GetRequiredService<IRepository<EducationMedium, Guid>>(), yearId, mediumId, EducationBoardType.CBSE, marker);
            var groups = Enumerable.Range(0, 16).Select(index =>
            {
                var group = new ElectiveGroup(Guid.NewGuid(), null, $"{marker} {index}", "#00A76F", mediumId);
                typeof(ElectiveGroup).GetProperty(nameof(ElectiveGroup.CreationTime))!.SetValue(group, createdAt);
                typeof(ElectiveGroup).GetProperty(nameof(ElectiveGroup.CreatorId))!.SetValue(group, actorId);
                typeof(ElectiveGroup).GetProperty(nameof(ElectiveGroup.LastModificationTime))!.SetValue(group, updatedAt);
                typeof(ElectiveGroup).GetProperty(nameof(ElectiveGroup.LastModifierId))!.SetValue(group, index % 2 == 0 ? actorId : otherActorId);
                return group;
            }).ToList();
            await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertManyAsync(groups, autoSave: true);
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            db.ChangeTracker.Clear();
            var service = GetRequiredService<IAcademicAuditTrailAppService>();

            AcademicAuditEventListRequestDto Request(string? search, int skip = 2, int count = 5) => new()
            {
                AcademicYearId = yearId,
                AcademicMediumId = mediumId,
                SubModuleCode = "elective_group_creation",
                ActorUserId = filterActor ? actorId : null,
                FromDate = new DateTime(2026, 10, 15),
                ToDate = new DateTime(2026, 10, 25),
                Sorting = $"occurredAt {direction}",
                Search = search,
                SkipCount = skip,
                MaxResultCount = count
            };

            var unbounded = await service.GetListAsync(Request(marker, skip: 0, count: 100));
            var bounded = await service.GetListAsync(Request(null));
            unbounded.TotalCount.ShouldBe(filterActor ? 24 : 32);
            bounded.TotalCount.ShouldBe(unbounded.TotalCount);
            bounded.Items.Select(item => item.Id).ShouldBe(unbounded.Items.Skip(2).Take(5).Select(item => item.Id));
            bounded.Items.ShouldAllBe(item => item.SourceAuditLogId == null && item.Source == "elective_group_entity");
            if (filterActor) bounded.Items.ShouldAllBe(item => item.ActorUserId == actorId);

            var olderEvent = unbounded.Items.First(item => item.Action == "created");
            var detail = await service.GetAsync(olderEvent.Id);
            detail.Id.ShouldBe(olderEvent.Id);
            detail.EntityId.ShouldBe(olderEvent.EntityId);
            detail.Action.ShouldBe("created");
            detail.ActorUserId.ShouldBe(actorId);
            detail.OccurredAt.ShouldBe(createdAt);
        });
    }
}
