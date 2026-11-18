using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class PaymentTermScheduleReadBatch_Tests
{
    [Fact]
    public async Task Shared_term_is_read_once_for_all_heads_without_changing_their_schedules()
    {
        var term = new PaymentTerm(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Shared term", "SHARED", null,
            false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
        var version = new PaymentTermVersion(Guid.NewGuid(), null, term.Id, 1, PaymentTermVersionStatus.Published,
            HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
        version.Installments.Add(new PaymentTermInstallment(Guid.NewGuid(), version.Id, 1, "Annual",
            new(2026, 4, 1), new(2027, 3, 31), new(2026, 4, 10), new(2026, 4, 10), 100m, true, false));
        term.Versions.Add(version);
        var repository = Substitute.For<IRepository<PaymentTerm, Guid>>();
        repository.FindAsync(term.Id, Arg.Any<bool>(), default).Returns(term);
        repository.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PaymentTerm, bool>>>(), Arg.Any<bool>(), default)
            .Returns([term]);
        var structure = new FeeStructure(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Batch read");
        for (var index = 0; index < 10; index++)
        {
            var detail = new FeeStructureDetail(Guid.NewGuid(), null, structure.Id, Guid.NewGuid(), 1000m + index,
                false, false, null, null);
            detail.AttachPaymentTerm(term.Id, version.Id);
            structure.AddDetail(detail);
        }

        var schedules = await new PaymentTermScheduleResolver(repository).ResolveAllAsync(structure);

        Assert.Equal(10, schedules.Count);
        Assert.All(schedules, schedule =>
        {
            Assert.True(schedule.IsValid);
            Assert.Equal(version.Id, schedule.PaymentTermVersionId);
            Assert.Equal(100m, Assert.Single(schedule.Installments).AllocationPercentage);
        });
        Assert.Equal(10045m, schedules.Sum(schedule => schedule.AnnualAmount));
        var databaseReads = repository.ReceivedCalls().Count(call => call.GetMethodInfo().Name is "FindAsync" or "GetAsync" or "GetListAsync");
        Assert.Equal(1, databaseReads);
    }
}
