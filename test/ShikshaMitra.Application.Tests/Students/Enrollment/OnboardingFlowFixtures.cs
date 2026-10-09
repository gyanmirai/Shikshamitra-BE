using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Shouldly;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    private async Task<AdmissionCertificationFixture> CreateOnboardingFlowFixtureAsync(string name, AdmissionDocumentPolicy? documents, bool prepareAccess = false, bool automatic = false)
    {
        await WithUnitOfWorkAsync(async () => {
            var roles = GetRequiredService<Volo.Abp.Identity.IdentityRoleManager>();
            if (!await roles.RoleExistsAsync("admin")) (await roles.CreateAsync(new Volo.Abp.Identity.IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
            var users = GetRequiredService<Volo.Abp.Identity.IdentityUserManager>();
            var user = (await users.FindByIdAsync(TestUserId.ToString()))!;
            if (!await users.IsInRoleAsync(user, "admin")) (await users.AddToRoleAsync(user, "admin")).Succeeded.ShouldBeTrue();
        });
        var entry = Guid.NewGuid(); var decision = Guid.NewGuid();
        var workflow = await _workflowAppService.CreateAsync(new()
        {
            Name = "Onboarding offering journey", Code = $"ONBOARD_{Guid.NewGuid():N}"[..24],
            Stages = [new() { Id = entry, Code = documents == null ? "FEES" : "DOCUMENTS", Name = "Admission requirements",
                StageType = documents == null ? AdmissionWorkflowStageType.Payment : AdmissionWorkflowStageType.DocumentVerification,
                IsEntryStage = true, IsRequired = true, ConfigurationJson = documents == null ? null : JsonSerializer.Serialize(new { documentVerification = documents }) },
                new() { Id = decision, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
                    DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }],
            Transitions = [new() { FromStageId = entry, ToStageId = decision, IsDefault = true, Priority = 1 }]
        });
        workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
        // Persist a historical document collection policy to cover legacy original/both evidence at the new Offering handoff.
        if (documents != null)
            await WithUnitOfWorkAsync(async () => {
                var repository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
                var stage = await repository.GetAsync(workflow.Stages.Single(s => s.IsEntryStage).Id);
                stage.Update(stage.Code, stage.Name, stage.StageType, stage.DisplayOrder, stage.IsRequired, false,
                    stage.Description, JsonSerializer.Serialize(new { documentVerification = documents with { AllowWaivers = false } }), true, false);
                await repository.UpdateAsync(stage, true);
            });
        var offering = await CreateAvailableOfferingAsync(workflow.Id);
        var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
        await ConfigureOfferingReadinessAsync(offering, cycle.AcademicYearId, prepareAccess, automatic);
        var applicant = await CreateCertificationApplicantAsync(name, true, true, null);
        var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
        await _applicationAppService.SubmitAsync(application.Id);
        if (documents == null) application = await CompleteOnboardingDecisionAsync(application.Id);
        return new AdmissionCertificationFixture(applicant, offering, application, null);
    }

    private async Task AssertLockedGroupNewEntrantAsync(IStudentOnboardingAppService service, AdmissionOffering existingOffering, Guid yearId, Guid occupiedDivisionId)
    {
        var division = await WithUnitOfWorkAsync(async () => {
            var value = await GetRequiredService<DivisionManager>().CreateAsync(existingOffering.StandardId, "LockedEntrantsC", 40);
            return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
        });
        // Group policy exists before any case or Records student is placed in this division.
        await service.SetIdentifierLockAsync(new() { AcademicYearId = yearId, DivisionId = division.Id, IdentifierType = "gr", IsLocked = true });
        await service.SetIdentifierLockAsync(new() { AcademicYearId = yearId, DivisionId = division.Id, IdentifierType = "roll", IsLocked = true });
        var entrant = await CreateOnboardingFlowFixtureAsync("LockedEntrant", null);
        entrant.Offering.StandardId.ShouldBe(existingOffering.StandardId);
        var settings = (await service.GetSettingsAsync(yearId)).Single(s => s.AcademicYearId == yearId);
        var configuration = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
        configuration["grNumber"]!["assignmentMode"] = "manual";
        configuration["rollNumber"]!["assignmentMode"] = "manual";
        await service.SaveSettingsAsync(new() { AcademicYearId = yearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
        var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(entrant.Application.Id), true, "locked-new-entry");
        var item = await service.GetAsync(finalized.OnboardingCaseId);
        item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
        item.GrNumber.ShouldBe("1002"); item.RollNumber.ShouldBe(1);
        item.GrNumberLocked.ShouldBeTrue(); item.RollNumberLocked.ShouldBeTrue();
        var refreshed = await service.GetAsync(item.Id);
        refreshed.GrNumber.ShouldBe(item.GrNumber); refreshed.RollNumber.ShouldBe(1);
        (await Should.ThrowAsync<BusinessException>(() => service.AssignRollNumberAsync(item.Id,
            new() { RollNumber = 2, RollAssignmentMode = "manual", ConcurrencyStamp = item.ConcurrencyStamp, Reason = "Cannot change locked roll" })))
            .Code.ShouldBe("Students:Onboarding:IdentifierLocked");
        var free = await WithUnitOfWorkAsync(async () => {
            var value = await GetRequiredService<DivisionManager>().CreateAsync(existingOffering.StandardId, "UnlockedPlacementD", 40);
            return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
        });
        item = await service.SetPlacementAsync(item.Id, new() { DivisionId = free.Id, ConcurrencyStamp = item.ConcurrencyStamp });
        item.DivisionId.ShouldBe(free.Id); item.RollNumber.ShouldBe(1); item.RollNumberLocked.ShouldBeTrue();
        await Should.ThrowAsync<BusinessException>(() => service.SetPlacementAsync(item.Id,
            new() { DivisionId = occupiedDivisionId, ConcurrencyStamp = item.ConcurrencyStamp }));
        (await service.GetAsync(item.Id)).DivisionId.ShouldBe(free.Id);
    }

    private async Task<DocumentHandoffFixture> CreateOnboardingDocumentFixtureAsync(AdmissionDocumentPolicy policy)
    {
        var fixture = await CreateOnboardingFlowFixtureAsync("OnboardDocument", policy);
        var runtime = await _applicationAppService.GetWorkflowAsync(fixture.Application.Id);
        return new DocumentHandoffFixture(fixture.Application, fixture.Offering, runtime.CurrentStageInstanceId!.Value, policy, null);
    }

    private async Task<AdmissionApplicationDto> CompleteOnboardingDecisionAsync(Guid applicationId)
    {
        await CompleteCertificationWorkflowAsync(applicationId, stopBeforeApproval: true);
        var application = await _applicationAppService.GetAsync(applicationId);
        application = await _applicationAppService.ApproveAsync(applicationId, Decision(application, "Admission approved"));
        await CompleteCertificationWorkflowAsync(applicationId);
        return await _applicationAppService.GetAsync(applicationId);
    }
}
