using System;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayrollDocumentIdentityFactAttribute : FactAttribute
{
    public PayrollDocumentIdentityFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_DOCUMENT_IDENTITY_TEST_POSTGRES")))
            Skip = "Set PAYROLL_DOCUMENT_IDENTITY_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
    }
}
public sealed class PayrollDocumentIdentityFactory : PayslipPostgresTestFactory
{
    protected override string ConnectionVariable => "PAYROLL_DOCUMENT_IDENTITY_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayrollDocumentIdentity_Tests
{
    [PayrollDocumentIdentityFact]
    public async Task Issuance_uses_institute_identity_and_replacement_preserves_the_original_pdf()
    {
        using var factory = new PayrollDocumentIdentityFactory();
        var workflow = new EsiPayrollWorkflowScenario(factory);
        var employee = await EmployeeTestHelper.CreateEmployeeAsync(workflow.Api, "InstituteDocumentIdentity");
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        Guid documentId;
        Guid slipId;
        string originalHash;
        using (var scope = factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var services = scope.ServiceProvider;
            var tenants = services.GetRequiredService<ITenantRepository>();
            var profile = await tenants.GetAsync(tenantId);
            profile.SetProperty("InstituteName", "Institute Source School");
            profile.SetProperty("Address1", "Institute Source Road");
            await tenants.UpdateAsync(profile, true);
            var settings = services.GetRequiredService<IRepository<PayslipSettings, Guid>>();
            var preferences = await settings.GetAsync(tenantId);
            // Existing payroll identity and logo overrides must not remain an alternative source.
            preferences.Configure("Stale Payroll School", "Stale Payroll Road", false, Guid.NewGuid(), new()
            { Layout = "Compact", LogoBlobName = $"payroll-logos/{tenantId}/{Guid.NewGuid():N}" });
            await settings.UpdateAsync(preferences, true);
            var evidence = new PayrollRunItemSnapshot(Guid.NewGuid(), tenantId, "2026-10", employee.Id)
            { BasisDays = 31m, TotalWorkingDays = 31m, PayableDays = 31m, ProrationFactor = 1m };
            var slip = await services.GetRequiredService<SalarySlipSnapshotWriter>()
                .GenerateOrGetSalarySlipFromSnapshotAsync(evidence, [new SalarySlipLineItem("Basic", 10000m)], [], [],
                    ShikshaMitra.HR.Payroll.Settings.DayCalculationType.TotalMonthDays);
            slip.FinalizeSlip();
            await services.GetRequiredService<ISalarySlipRepository>().UpdateAsync(slip, true);
            var issued = await services.GetRequiredService<PayslipIssuanceService>().IssueAsync(slip, snapshotEvidence: evidence);
            var facts = JsonSerializer.Deserialize<SalarySlipDto>(issued.FactsJson)!;
            facts.Presentation!.SchoolName.ShouldBe("Institute Source School");
            facts.Presentation.SchoolAddress.ShouldContain("Institute Source Road");
            facts.Presentation.Options.Layout.ShouldBe("Compact");
            facts.Presentation.Options.LogoBlobName.ShouldBeNull();
            originalHash = Convert.ToHexString(SHA256.HashData(issued.Pdf));
            originalHash.ShouldBe(issued.Sha256);
            documentId = issued.Id; slipId = slip.Id;
            await unit.CompleteAsync();
        }
        using (var scope = factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var services = scope.ServiceProvider;
            var tenants = services.GetRequiredService<ITenantRepository>();
            var profile = await tenants.GetAsync(tenantId);
            profile.SetProperty("InstituteName", "Updated Institute School");
            profile.SetProperty("Address1", "Updated Institute Road");
            await tenants.UpdateAsync(profile, true);
            var documents = services.GetRequiredService<IRepository<PayslipDocument, Guid>>();
            var previous = await documents.GetAsync(documentId);
            var slip = await services.GetRequiredService<ISalarySlipRepository>().GetAsync(slipId);
            var replacement = await services.GetRequiredService<PayslipIssuanceService>()
                .ReplaceFactsAsync(slip, previous, Guid.NewGuid(), "School identity updated in Institute Setup");
            var facts = JsonSerializer.Deserialize<SalarySlipDto>(replacement.FactsJson)!;
            facts.Presentation!.SchoolName.ShouldBe("Updated Institute School");
            facts.Presentation.SchoolAddress.ShouldContain("Updated Institute Road");
            replacement.ReplacesDocumentId.ShouldBe(previous.Id);
            Convert.ToHexString(SHA256.HashData(previous.Pdf)).ShouldBe(originalHash);
            JsonSerializer.Deserialize<SalarySlipDto>(previous.FactsJson)!.Presentation!.SchoolName.ShouldBe("Institute Source School");
            await unit.CompleteAsync();
        }
        var originalPdf = await workflow.Api.GetByteArrayAsync($"/api/payroll/payslip-documents/{documentId}/pdf");
        Convert.ToHexString(SHA256.HashData(originalPdf)).ShouldBe(originalHash);
        factory.Messages.ShouldBeEmpty();
    }
}
