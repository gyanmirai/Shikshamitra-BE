using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Assessment_catalog_returns_active_medium_subjects_with_published_class_suggestions_and_denies_missing_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var subjects = GetRequiredService<IRepository<Subject, Guid>>();
			var active = await subjects.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Entrance language", "", offering.MediumId), true);
			var unmapped = await subjects.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Prior grade syllabus", "", offering.MediumId), true);
			var inactive = await subjects.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Inactive entrance", "", offering.MediumId, status: SubjectMasterStatus.Inactive), true);
			var assignment = new AssignedSubject(Guid.NewGuid(), TestTenantId, offering.StandardId, active.Id, "ENTRANCE_LANGUAGE", SubjectStatus.Published);
			await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assignment, true);
			var service = GetRequiredService<IAdmissionWorkflowAppService>();
			var catalog = await service.GetAssessmentCatalogAsync();
			catalog.Classes.ShouldContain(item => item.Id == offering.StandardId && item.MediumId == offering.MediumId && item.BoardId == offering.BoardId);
			catalog.Subjects.Single(item => item.Id == active.Id).AssignedStandardIds.ShouldContain(offering.StandardId);
			catalog.Subjects.Single(item => item.Id == unmapped.Id).AssignedStandardIds.ShouldBeEmpty();
			catalog.Subjects.ShouldNotContain(item => item.Id == inactive.Id);
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"assessment-catalog-{userId:N}");
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			var empty = await service.GetAssessmentCatalogAsync();
			empty.Classes.ShouldBeEmpty();
			empty.Subjects.ShouldBeEmpty();
			var scopedUser = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(scopedUser, $"assessment-class-{scopedUser:N}");
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: scopedUser,
				academicYearId: year, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), true);
			using var scopedPrincipal = _principalAccessor.Change(ProfilePrincipal(scopedUser));
			var scopedCatalog = await service.GetAssessmentCatalogAsync();
			scopedCatalog.Classes.Select(item => item.Id).ShouldBe([offering.StandardId]);
			scopedCatalog.Subjects.ShouldContain(item => item.Id == unmapped.Id);
			using var foreignTenant = _currentTenant.Change(Guid.NewGuid());
			var foreignCatalog = await service.GetAssessmentCatalogAsync();
			foreignCatalog.Classes.ShouldBeEmpty();
			foreignCatalog.Subjects.ShouldBeEmpty();
		});

	[Fact]
	public Task Assessment_configuration_rejects_cross_medium_subjects_and_invalid_class_hierarchy_but_allows_custom_subjects() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var subject = await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "School subject", "", offering.MediumId), true);
			var profile = new AdmissionAssessmentClassProfile
			{
				Id = "profile", Name = "Entrance", BoardId = offering.BoardId, MediumId = offering.MediumId, StandardIds = [offering.StandardId],
				Subjects = [new() { Code = "school", SubjectId = subject.Id, Name = subject.Name, TotalMarks = 50 }, new() { Code = "custom", Name = "Aptitude", TotalMarks = 50 }],
				PassRule = "overall", PassingMarks = 40
			};
			var policy = new AdmissionAssessmentPolicy { Version = 2, Enabled = true, ScoringEnabled = true, ScoringMode = "subjects", ClassProfiles = [profile], MaxAttempts = 2 };
			AdmissionWorkflowStageDefinition Stage(AdmissionAssessmentPolicy input) => new(Guid.NewGuid(), offering.WorkflowDefinitionId!.Value,
				"ENTRANCE", "Entrance", AdmissionWorkflowStageType.Assessment, 0, true, false, null, JsonSerializer.Serialize(new { assessment = input }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
			var validator = GetRequiredService<AdmissionAssessmentConfigurationService>();
			var validIssues = await WithUnitOfWorkAsync(() => validator.ValidateAsync(Stage(policy)));
			validIssues.ShouldBeEmpty(string.Join(" | ", validIssues.Select(item => item.Message)));
			(await WithUnitOfWorkAsync(() => validator.ValidateAsync(Stage(policy with { ClassProfiles = [profile with { StandardIds = [Guid.NewGuid()] }] })))).ShouldContain(item => item.Code == AdmissionAssessmentErrorCodes.InvalidPolicy);
			(await WithUnitOfWorkAsync(() => validator.ValidateAsync(Stage(policy with { ClassProfiles = [profile with { Subjects = [profile.Subjects[0] with { SubjectId = Guid.NewGuid() }] }] })))).ShouldContain(item => item.Message.Contains("unavailable"));
			(await WithUnitOfWorkAsync(() => validator.ValidateAsync(Stage(policy with { EffectiveProfile = profile })))).ShouldContain(item => item.Message.Contains("resolved"));
		});

	[Fact]
	public Task School_subject_used_only_in_admission_assessment_is_protected_and_reported_in_usage() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var subjects = GetRequiredService<IRepository<Subject, Guid>>();
			var subject = await subjects.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Admission-only school subject", "", offering.MediumId), true);
			var workflow = await GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, "Subject references", $"REF_{Guid.NewGuid():N}"[..24]), true);
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id,
				"ENTRANCE", "Entrance", AdmissionWorkflowStageType.Assessment, 0, true, false, null,
				JsonSerializer.Serialize(new { assessment = new { classProfiles = new[] { new { subjects = new[] { new { subjectId = subject.Id } } } } } })), true);
			var service = GetRequiredService<ISubjectAppService>();
			var usage = await service.CheckUsagesAsync(subject.Id);
			usage.AdmissionAssessments.ShouldBe(1);
			usage.HasUsages.ShouldBeTrue();
			(await service.GetUsageDetailsAsync(subject.Id)).CanDelete.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => service.DeleteAsync(subject.Id))).Code.ShouldBe(CurriculumErrorCodes.SubjectIsInUse);
		});

	[Theory]
	[InlineData("ClassProfiles", "Subjects", "SubjectId")]
	[InlineData("cLaSsPrOfIlEs", "sUbJeCtS", "SuBjEcTiD")]
	public Task Assessment_subject_usage_matches_case_insensitive_policy_fields_and_blocks_deletion(string profilesName, string subjectsName, string subjectIdName) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var subject = await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Mixed case entrance", "", offering.MediumId), true);
			var profile = new Dictionary<string, object>
			{
				["Id"] = "mixed-case", ["Name"] = "Entrance", ["BoardId"] = offering.BoardId, ["MediumId"] = offering.MediumId,
				["StandardIds"] = new[] { offering.StandardId }, ["PassRule"] = "overall", ["PassingMarks"] = 40,
				[subjectsName] = new[] { new Dictionary<string, object> { ["Code"] = "language", ["Name"] = subject.Name, ["TotalMarks"] = 100, [subjectIdName] = subject.Id } }
			};
			var configuration = JsonSerializer.Serialize(new { assessment = new Dictionary<string, object>
			{
				["Version"] = 2, ["Enabled"] = true, ["ScoringEnabled"] = true, ["ScoringMode"] = "subjects", ["MaxAttempts"] = 1, [profilesName] = new[] { profile }
			} });
			AdmissionAssessmentPolicy.Read(configuration)!.ClassProfiles.Single().Subjects.Single().SubjectId.ShouldBe(subject.Id);
			var workflow = await GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId,
				"Mixed case references", $"MIXED_{Guid.NewGuid():N}"[..24]), true);
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id,
				"ENTRANCE", "Entrance", AdmissionWorkflowStageType.Assessment, 0, true, false, null, configuration), true);
			var service = GetRequiredService<ISubjectAppService>();
			(await service.CheckUsagesAsync(subject.Id)).AdmissionAssessments.ShouldBe(1);
			(await service.GetUsageDetailsAsync(subject.Id)).CanDelete.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => service.DeleteAsync(subject.Id))).Code.ShouldBe(CurriculumErrorCodes.SubjectIsInUse);
		});
}
