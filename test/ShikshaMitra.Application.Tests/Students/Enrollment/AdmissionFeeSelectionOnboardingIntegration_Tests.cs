using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    private async Task ConfigureOfferingReadinessAsync(AdmissionOffering offering, Guid yearId, bool prepareAccess = false, bool automatic = false)
    {
        var coverage = new { boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all" };
        var rules = new { requireDocuments = true, studentAccount = prepareAccess, guardianAccount = false, approvalMode = "none", credentialProvisioningMode = automatic ? "automatic" : "manual", sendStudentCredentialEmail = automatic };
        var config = JsonSerializer.Serialize(new
        {
            readinessAccess = rules,
            onboardingProfiles = new[] { new { id = "offering-ready", offeringIds = new[] { offering.Id }, applicability = coverage, rules } },
            grNumber = new { assignmentMode = "both", configurations = new[] { new
            {
                id = "gr-fixture", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all",
                status = "published", version = 1, isActive = true, startingNumber = 1001, padding = 4, formatPattern = "{PREFIX}{SEQUENCE}{SUFFIX}", manualEntryAllowed = true
            } } },
            rollNumber = new { assignmentMode = "automatic", configurations = Array.Empty<object>() },
            divisionChange = new { configurations = Array.Empty<object>() }
        });
        var service = GetRequiredService<IStudentOnboardingAppService>();
        var settings = (await service.GetSettingsAsync(yearId)).SingleOrDefault(s => s.AcademicYearId == yearId);
        var saved = System.Text.Json.Nodes.JsonNode.Parse(config)!.AsObject();
        var row = saved["grNumber"]!["configurations"]![0]!;
        var initial = settings == null || !JsonDocument.Parse(settings.ConfigurationJson).RootElement.TryGetProperty("grNumber", out _);
        if (initial) { row["status"] = "draft"; row["isActive"] = false; }
        var result = await service.SaveSettingsAsync(new() { AcademicYearId = yearId, ConfigurationJson = saved.ToJsonString(), ConcurrencyStamp = settings?.ConcurrencyStamp });
        if (initial)
        {
            row["status"] = "published"; row["isActive"] = true;
            await service.SaveSettingsAsync(new() { AcademicYearId = yearId, ConfigurationJson = saved.ToJsonString(), ConcurrencyStamp = result.ConcurrencyStamp });
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Offering_fee_reference_confirmation_should_gate_activation_without_finance_payment_or_assignment(bool prepareAccess, bool failAutomaticAccounts) =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateOnboardingFlowFixtureAsync("FeeConfirm", null, prepareAccess, failAutomaticAccounts);
            var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
            var structure = await CreateFeeSelectionReferenceAsync(fixture.Offering, cycle.AcademicYearId);
            await ConfigureOfferingReadinessAsync(fixture.Offering, cycle.AcademicYearId, prepareAccess, failAutomaticAccounts);
            var service = GetRequiredService<IStudentOnboardingAppService>();
            await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Offering readiness", RequireDocuments = false,
                RequireFamilyDetails = true, RequireCommunicationDetails = true, RequireMedicalDetails = true, RequireServiceConsent = true, RequireStudentAccount = false, RequireGuardianAccount = false });
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "fee-reference-finalize");
            var division = await WithUnitOfWorkAsync(async () => {
                var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "FeeReferenceA", 40);
                return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
            });
            var item = await service.GetAsync(finalized.OnboardingCaseId);
            item.RequirementsNeedReview.ShouldBeFalse();
            item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Documents).Status.ShouldBe(StudentOnboardingStepStatus.Completed);
            item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
            // Roll preparation succeeds independently before a GR series has been assigned.
            item = await service.AssignRollNumberAsync(item.Id, new() { RollNumber = 1, RollAssignmentMode = "automatic", ConcurrencyStamp = item.ConcurrencyStamp });
            item.RollNumber.ShouldBe(1);
            item.GrNumber.ShouldBeNullOrEmpty();
            var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
            item = await service.AssignGrNumberAsync(item.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = item.ConcurrencyStamp });
            item.GrNumber.ShouldBe("1001");
            item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
            (await service.PreviewActivationAsync(item.Id)).LastError.ShouldContain("fee structure");
            await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id, new() { IdempotencyKey = "fee-reference-blocked", ConcurrencyStamp = item.ConcurrencyStamp }));
            item = await service.GetAsync(item.Id);
            var selection = await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
            selection.OnboardingConfirmed.ShouldBeTrue();
            item = await service.GetAsync(item.Id);
            var failure = GetRequiredService<OnboardingAccountFailureProbe>();
            if (failAutomaticAccounts) failure.CaseId = item.Id;
            item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "fee-reference-active", ConcurrencyStamp = item.ConcurrencyStamp });
            failure.CaseId = null;
            if (failAutomaticAccounts) { failure.Attempts.ShouldBeGreaterThan(0); item.Events.ShouldContain(e => e.EventType == "AccessPending"); }
            item.StudentEnrollmentId.ShouldNotBeNull();
            item.Status.ShouldBe(prepareAccess ? StudentOnboardingStatus.NeedsAttention : StudentOnboardingStatus.Completed, item.LastError);
            item.CredentialsPending.ShouldBe(prepareAccess);
            (await service.GetServiceHandoffsAsync(item.Id)).ShouldNotContain(h => h.ServiceKey == "service-consent");
            item.Steps.Where(s => s.Key is StudentOnboardingStepKeys.Identity or StudentOnboardingStepKeys.Family or StudentOnboardingStepKeys.Communication or StudentOnboardingStepKeys.MedicalAndPreviousSchool)
                .ShouldAllBe(s => !s.Required);
            // Reload through canonical Records query and profile access after persisted activation.
            var directory = await GetRequiredService<ShikshaMitra.Students.Records.IStudentDirectoryAppService>().GetListAsync(new()
                { AcademicYearId = cycle.AcademicYearId, StudentId = finalized.StudentId, Population = "Active", MaxResultCount = 10 });
            directory.Items.Single().StudentId.ShouldBe(finalized.StudentId);
            (await WithUnitOfWorkAsync(() => GetRequiredService<ShikshaMitra.Students.Records.StudentRecordsAccessService>().EnsureReadableAsync(finalized.StudentId))).Access.ShouldBe("Full");
            item.FirstPaymentConfirmed.ShouldBeNull();
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(item.StudentEnrollmentId!.Value))).StudentId.ShouldBe(finalized.StudentId);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync(x => x.StudentId == finalized.StudentId))).ShouldBe(0);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().CountAsync(x => x.StudentId == finalized.StudentId))).ShouldBe(0);
            // GR and Roll locks remain independent after the enrollment handoff, including Records-only groups.
            var students = GetRequiredService<IRepository<ShikshaMitra.Students.Student, Guid>>();
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = division.Id, IdentifierType = "gr", IsLocked = true });
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = division.Id, IdentifierType = "roll", IsLocked = true });
            (await WithUnitOfWorkAsync(() => students.GetAsync(finalized.StudentId))).IsIdentifierLocked("gr").ShouldBeTrue();
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = division.Id, IdentifierType = "gr", IsLocked = false, Reason = "Correct identifier" });
            var unlocked = await WithUnitOfWorkAsync(() => students.GetAsync(finalized.StudentId));
            unlocked.IsIdentifierLocked("gr").ShouldBeFalse(); unlocked.IsIdentifierLocked("roll").ShouldBeTrue();
            (await service.GetAsync(item.Id)).GrNumberLocked.ShouldBeFalse();
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = division.Id, IdentifierType = "roll", IsLocked = false, Reason = "Change roll" });
            var recordsDivision = await WithUnitOfWorkAsync(async () => {
                var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "RecordsReferenceB", 40);
                await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
                var stored = await students.GetAsync(finalized.StudentId); stored.AssignDivision(value.Id); await students.UpdateAsync(stored, true);
                return value;
            });
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = division.Id, IdentifierType = "gr", IsLocked = true });
            (await WithUnitOfWorkAsync(() => students.GetAsync(finalized.StudentId))).IsIdentifierLocked("gr").ShouldBeFalse();
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = recordsDivision.Id, IdentifierType = "gr", IsLocked = true });
            (await WithUnitOfWorkAsync(() => students.GetAsync(finalized.StudentId))).IsIdentifierLocked("gr").ShouldBeTrue();
            await service.SetIdentifierLockAsync(new() { AcademicYearId = cycle.AcademicYearId, DivisionId = recordsDivision.Id, IdentifierType = "gr", IsLocked = false, Reason = "Records correction" });
            (await WithUnitOfWorkAsync(() => students.GetAsync(finalized.StudentId))).IsIdentifierLocked("gr").ShouldBeFalse();
            await WithUnitOfWorkAsync(async () => {
                var stored = await students.GetAsync(finalized.StudentId); stored.AssignGrNumber("QA-1001", null, "Preserve imported formatted identifier");
                await students.UpdateAsync(stored, true);
            });
            (await GetRequiredService<ShikshaMitra.Students.Records.IStudentAppService>().GetStudentsByDivisionAsync(recordsDivision.Id)).Items.Single().GrNumber.ShouldBe("QA-1001");
            await AssertLockedGroupNewEntrantAsync(service, fixture.Offering, cycle.AcademicYearId, division.Id);
        });
}
