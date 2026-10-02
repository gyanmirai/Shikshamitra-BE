using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiConfigurationResolution_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{

    [Fact]
    public async Task New_school_baseline_is_on_idempotent_and_tenant_isolated()
    {
        var tenant = GetRequiredService<ICurrentTenant>();
        Guid firstId;
        using (tenant.Change(Guid.NewGuid()))
        {
            firstId = await WithUnitOfWorkAsync(async () => {
                var resolver = GetRequiredService<EsiPayrollConfigurationResolver>();
                await resolver.EnsureDefaultAsync();
                await resolver.EnsureDefaultAsync();
                var versions = await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().GetListAsync();
                versions.Count.ShouldBe(1);
                var configuration = versions[0];
                configuration.Enabled.ShouldBeTrue();
                configuration.RegistrationNumber.ShouldBeNull();
                configuration.CoveredSince.ShouldBeNull();
                configuration.ToCalculationEstablishment()!.Status.ShouldBe(StatutoryEstablishmentStatus.Active);
                return configuration.Id;
            });
        }
        using (tenant.Change(Guid.NewGuid()))
        {
            await WithUnitOfWorkAsync(async () => {
                var configuration = await GetRequiredService<EsiPayrollConfigurationResolver>().ResolveAsync(new(2026, 9, 1));
                configuration!.Id.ShouldNotBe(firstId);
                configuration.Enabled.ShouldBeTrue();
            });
        }
    }
    [Fact]
    public async Task Invalidation_preserves_finalized_slips_even_if_their_snapshot_is_unlocked()
    {
        var employee = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () => {
            await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
                new PayrollRunItemSnapshot(snapshotId, null, "2090-01", employee));
            var slip = new SalarySlip(Guid.NewGuid(), null, employee, "2090-01", [], [], DateTime.UtcNow);
            slip.FinalizeSlip();
            await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
        });
        await WithUnitOfWorkAsync(async () => {
            await GetRequiredService<EsiConfigurationPeriodGuard>().InvalidateAsync(new(2090, 1, 1), null);
            (await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().AnyAsync(x => x.Id == snapshotId)).ShouldBeTrue();
            (await GetRequiredService<IRepository<SalarySlip, Guid>>().GetAsync(x => x.EmployeeId == employee)).Status.ShouldBe(SalarySlipStatus.Finalized);
        });
    }

	[Fact]
	public async Task Reenable_continues_locked_coverage_only_within_the_contribution_period()
	{
		var employeeId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-08", employeeId)
			{
				EmployeeStateInsuranceRuleId = Guid.NewGuid(),
				EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
				EmployeeStateInsuranceNumber = "TEST-ESI-001"
			};
			snapshot.MarkLocked();
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var resolver = GetRequiredService<EsiEnrollmentResolver>();
			var september = (await resolver.ResolveAsync(new(2026, 9, 1), [employeeId]))[employeeId];
			september.Status.ShouldBe(EmployeeStatutoryEnrollmentStatus.Covered);
			september.ContributionPeriodEnd.ShouldBe(new DateTime(2026, 9, 30));
			september.InsuranceNumber.ShouldBe("TEST-ESI-001");
			(await resolver.ResolveAsync(new(2026, 10, 1), [employeeId])).ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task Month_boundary_selects_scheduled_version_and_cancellation_restores_previous_version()
	{
		var previous = new EsiPayrollConfiguration(Guid.NewGuid(), null, new(2080, 9, 1),
			true, "TEST-REG", new(2080, 9, 1), Guid.NewGuid().ToString());
		var scheduled = new EsiPayrollConfiguration(Guid.NewGuid(), null, new(2080, 10, 1),
			false, "TEST-REG", new(2080, 9, 1), Guid.NewGuid().ToString());
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
			await repository.InsertAsync(previous);
			await repository.InsertAsync(scheduled);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var resolver = GetRequiredService<EsiPayrollConfigurationResolver>();
			(await resolver.ResolveAsync(new(2080, 9, 30)))!.Id.ShouldBe(previous.Id);
			(await resolver.ResolveAsync(new(2080, 10, 1)))!.Id.ShouldBe(scheduled.Id);
			var repository = GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
			var current = await repository.GetAsync(scheduled.Id);
			current.Cancel("Rescheduled");
			await repository.UpdateAsync(current, autoSave: true);
			(await resolver.ResolveAsync(new(2080, 10, 1)))!.Id.ShouldBe(previous.Id);
		});
	}
}
