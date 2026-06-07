using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementWorkflow_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ProfessionalManagementTemplateWorkflow _templateWorkflow;
	private readonly ProfessionalManagementCycleWorkflow _cycleWorkflow;
	private readonly ProfessionalManagementReviewWorkflow _reviewWorkflow;
	private readonly ProfessionalManagementReviewContributionWorkflow _contributionWorkflow;
	private readonly ProfessionalManagementOutcomeWorkflow _outcomeWorkflow;
	private readonly IRepository<PerformanceTemplate, Guid> _templateRepository;
	private readonly IRepository<PerformanceTemplateSection, Guid> _sectionRepository;
	private readonly IRepository<PerformanceTemplateCriterion, Guid> _criterionRepository;
	private readonly IRepository<ProfessionalReviewCycle, Guid> _cycleRepository;
	private readonly IRepository<PerformanceReviewAssignment, Guid> _assignmentRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public ProfessionalManagementWorkflow_Tests()
	{
		_templateWorkflow = GetRequiredService<ProfessionalManagementTemplateWorkflow>();
		_cycleWorkflow = GetRequiredService<ProfessionalManagementCycleWorkflow>();
		_reviewWorkflow = GetRequiredService<ProfessionalManagementReviewWorkflow>();
		_contributionWorkflow = GetRequiredService<ProfessionalManagementReviewContributionWorkflow>();
		_outcomeWorkflow = GetRequiredService<ProfessionalManagementOutcomeWorkflow>();
		_templateRepository = GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		_sectionRepository = GetRequiredService<IRepository<PerformanceTemplateSection, Guid>>();
		_criterionRepository = GetRequiredService<IRepository<PerformanceTemplateCriterion, Guid>>();
		_cycleRepository = GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		_assignmentRepository = GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Template_Workflow_Should_Create_Publish_Clone_And_Archive_With_Indian_Metadata()
	{
		var tenantId = _guidGenerator.Create();
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var created = await _templateWorkflow.CreateTemplateAsync(BuildTemplateRequest("CBSE Workflow Template"));
				created.BoardType.ShouldBe(PerformanceBoardType.CBSE);
				created.GujaratiName.ShouldBe("સીબીએસઈ મૂલ્યાંકન");
				created.Sections.Sum(x => x.Weight).ShouldBe(100m);
				created.Sections.SelectMany(x => x.Criteria).ShouldContain(x =>
					x.EvidenceRequired &&
					x.SqaaDomain == PerformanceSqaaDomain.CurriculumPedagogyAssessment);

				var published = await _templateWorkflow.PublishTemplateAsync(created.Id);
				published.Status.ShouldBe(PerformanceTemplateStatus.Published);

				var clone = await _templateWorkflow.CloneTemplateAsync(created.Id);
				clone.Name.ShouldBe($"{created.Name} Copy");
				clone.Status.ShouldBe(PerformanceTemplateStatus.Draft);
				clone.BoardType.ShouldBe(created.BoardType);
				clone.Sections.First().GujaratiName.ShouldBe(created.Sections.First().GujaratiName);
				clone.Sections.First().Criteria.First().SqaaDomain.ShouldBe(created.Sections.First().Criteria.First().SqaaDomain);

				var archived = await _templateWorkflow.ArchiveTemplateAsync(created.Id);
				archived.Status.ShouldBe(PerformanceTemplateStatus.Archived);
			});
		}
	}

	[Fact]
	public async Task CreateReviewCycleAsync_Should_Reject_Invalid_Period()
	{
		var tenantId = _guidGenerator.Create();
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_cycleWorkflow.CreateReviewCycleAsync(new ProfessionalReviewCycleRequest
					{
						Name = "Invalid cycle",
						CycleType = PerformanceCycleType.Annual,
						PeriodStart = new DateTime(2026, 4, 1),
						PeriodEnd = new DateTime(2026, 3, 31),
						RatingScaleMax = 5,
						AllowNotApplicable = true
					}));

				exception.Code.ShouldBe(HRErrorCodes.ProfessionalReviewCycleInvalidPeriod);
			});
		}
	}

	[Fact]
	public async Task SubmitStageAsync_Should_Require_Evidence_For_Required_Criteria_Then_Save_Responses()
	{
		var scenario = await SeedReviewScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_reviewWorkflow.SaveStageAsync(
						scenario.AssignmentId,
						BuildStageRequest(scenario.RequiredCriterionId, scenario.OptionalCriterionId),
						submit: true));

				exception.Code.ShouldBe(HRErrorCodes.PerformanceEvidenceRequired);
			});

			await WithUnitOfWorkAsync(async () =>
			{
				await _contributionWorkflow.AddEvidenceAsync(
					scenario.AssignmentId,
					new PerformanceEvidenceRequest
					{
						CriterionId = scenario.RequiredCriterionId,
						Title = "Lesson plan evidence",
						Notes = "Uploaded by reviewer"
					});

				var review = await _reviewWorkflow.SaveStageAsync(
					scenario.AssignmentId,
					BuildStageRequest(scenario.RequiredCriterionId, scenario.OptionalCriterionId),
					submit: true);

				var selfStage = review.Stages.Single(x => x.StageType == PerformanceReviewStageType.SelfAppraisal);
				selfStage.Status.ShouldBe(PerformanceReviewStageStatus.Submitted);
				selfStage.Responses.Count.ShouldBe(2);
			});
		}
	}

	[Fact]
	public async Task SaveFinalOutcomeAsync_Should_Calculate_Weighted_Score_And_Finalize_Review()
	{
		var scenario = await SeedReviewScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await _contributionWorkflow.AddEvidenceAsync(
					scenario.AssignmentId,
					new PerformanceEvidenceRequest
					{
						CriterionId = scenario.RequiredCriterionId,
						Title = "Assessment evidence"
					});

				await _reviewWorkflow.SaveStageAsync(
					scenario.AssignmentId,
					BuildStageRequest(scenario.RequiredCriterionId, scenario.OptionalCriterionId),
					submit: true);

				var outcome = await _outcomeWorkflow.SaveFinalOutcomeAsync(
					scenario.AssignmentId,
					new PerformanceOutcomeRequest
					{
						FinalRating = "Very Good",
						Strengths = "Strong classroom practice",
						Recommendation = PerformanceRecommendationType.Increment,
						IncrementRecommended = true
					});

				outcome.FinalScore.ShouldBe(80m);
				outcome.IncrementRecommended.ShouldBeTrue();

				var assignment = await _assignmentRepository.GetAsync(scenario.AssignmentId);
				assignment.Status.ShouldBe(ProfessionalReviewStatus.Finalized);
				assignment.FinalScore.ShouldBe(80m);
			});
		}
	}

	private async Task<ReviewScenario> SeedReviewScenarioAsync()
	{
		var tenantId = _guidGenerator.Create();
		using (_currentTenant.Change(tenantId))
		{
			return await WithUnitOfWorkAsync(async () =>
			{
				var employee = await CreateEmployeeAsync(tenantId, "Teacher", "Mathematics Teacher");
				var template = new PerformanceTemplate(
					_guidGenerator.Create(),
					tenantId,
					$"CBSE Review Template {_guidGenerator.Create():N}",
					PerformanceStaffCategory.Teacher,
					null,
					boardType: PerformanceBoardType.CBSE);
				await _templateRepository.InsertAsync(template, autoSave: true);

				var section = new PerformanceTemplateSection(
					_guidGenerator.Create(),
					tenantId,
					template.Id,
					"Curriculum and Assessment",
					100,
					1,
					"પાઠયક્રમ અને મૂલ્યાંકન",
					PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _sectionRepository.InsertAsync(section, autoSave: true);

				var requiredCriterion = new PerformanceTemplateCriterion(
					_guidGenerator.Create(),
					tenantId,
					template.Id,
					section.Id,
					"Lesson plan evidence",
					null,
					50,
					true,
					1,
					"પાઠ આયોજન પુરાવો",
					PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _criterionRepository.InsertAsync(requiredCriterion, autoSave: true);

				var optionalCriterion = new PerformanceTemplateCriterion(
					_guidGenerator.Create(),
					tenantId,
					template.Id,
					section.Id,
					"Classroom delivery",
					null,
					50,
					false,
					2,
					"વર્ગખંડ શીખવણ",
					PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _criterionRepository.InsertAsync(optionalCriterion, autoSave: true);

				var cycle = new ProfessionalReviewCycle(
					_guidGenerator.Create(),
					tenantId,
					"Annual Review",
					new DateTime(2026, 1, 1),
					new DateTime(2026, 12, 31),
					ProfessionalReviewCycleStatus.InProgress);
				await _cycleRepository.InsertAsync(cycle, autoSave: true);

				var assignment = new PerformanceReviewAssignment(
					_guidGenerator.Create(),
					tenantId,
					cycle.Id,
					template.Id,
					employee.Id);
				await _assignmentRepository.InsertAsync(assignment, autoSave: true);

				return new ReviewScenario(
					tenantId,
					assignment.Id,
					requiredCriterion.Id,
					optionalCriterion.Id);
			});
		}
	}

	private async Task<Employee> CreateEmployeeAsync(Guid tenantId, string firstName, string designation)
	{
		var suffix = _guidGenerator.Create().ToString("N");
		var codePrefix = firstName[..Math.Min(firstName.Length, 3)].ToUpperInvariant();
		var roleId = _guidGenerator.Create();
		await _roleRepository.InsertAsync(
			new IdentityRole(roleId, $"pm-{codePrefix.ToLowerInvariant()}-{suffix[..8]}", tenantId),
			autoSave: true);

		var employee = new Employee(
			_guidGenerator.Create(),
			tenantId,
			userId: null,
			employeeCode: $"PM{codePrefix}{suffix[..10]}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Professional",
				FirstName: firstName,
				MiddleName: null,
				ShortName: firstName,
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: new DateOnly(1990, 1, 1),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"{firstName.ToLowerInvariant()}{suffix[..8]}@test.local",
				PanNo: UniquePan(suffix),
				AdharNo: UniqueAdhar(suffix),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: "Academics",
				Designation: designation,
				DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: null,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private static PerformanceTemplateRequest BuildTemplateRequest(string name) => new()
	{
		Name = name,
		GujaratiName = "સીબીએસઈ મૂલ્યાંકન",
		BoardType = PerformanceBoardType.CBSE,
		StaffCategory = PerformanceStaffCategory.Teacher,
		RatingScaleMax = 5,
		AllowNotApplicable = true,
		Sections =
		[
			new PerformanceTemplateSectionRequest
			{
				Name = "Curriculum and assessment",
				GujaratiName = "પાઠયક્રમ અને મૂલ્યાંકન",
				SqaaDomain = PerformanceSqaaDomain.CurriculumPedagogyAssessment,
				Weight = 60,
				DisplayOrder = 1,
				Criteria =
				[
					new PerformanceTemplateCriterionRequest
					{
						Name = "Lesson plans are board-aligned",
						GujaratiName = "પાઠ આયોજન બોર્ડ સાથે સુસંગત છે",
						SqaaDomain = PerformanceSqaaDomain.CurriculumPedagogyAssessment,
						Weight = 100,
						EvidenceRequired = true,
						DisplayOrder = 1
					}
				]
			},
			new PerformanceTemplateSectionRequest
			{
				Name = "Professional growth",
				SqaaDomain = PerformanceSqaaDomain.HumanResources,
				Weight = 40,
				DisplayOrder = 2,
				Criteria =
				[
					new PerformanceTemplateCriterionRequest
					{
						Name = "Training actions are completed",
						SqaaDomain = PerformanceSqaaDomain.HumanResources,
						Weight = 100,
						EvidenceRequired = false,
						DisplayOrder = 1
					}
				]
			}
		]
	};

	private static PerformanceReviewStageRequest BuildStageRequest(Guid requiredCriterionId, Guid optionalCriterionId) => new()
	{
		StageType = PerformanceReviewStageType.SelfAppraisal,
		SummaryComment = "Self review submitted",
		Responses =
		[
			new PerformanceReviewResponseRequest
			{
				CriterionId = requiredCriterionId,
				Rating = 5,
				IsNotApplicable = false,
				Comment = "Evidence-backed practice"
			},
			new PerformanceReviewResponseRequest
			{
				CriterionId = optionalCriterionId,
				Rating = 3,
				IsNotApplicable = false,
				Comment = "Good delivery"
			}
		]
	};

	private static string UniquePan(string suffix) =>
		$"PMTES{Math.Abs(suffix.GetHashCode()) % 10000:0000}A";

	private static string UniqueAdhar(string suffix) =>
		$"{Math.Abs(suffix.GetHashCode()) % 1000000000000:000000000000}";

	private sealed record ReviewScenario(
		Guid TenantId,
		Guid AssignmentId,
		Guid RequiredCriterionId,
		Guid OptionalCriterionId);
}
