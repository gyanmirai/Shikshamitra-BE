using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmPlaybookApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmPlaybookApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Quick_Setup_And_Advanced_Setup_Playbooks()
	{
		var quick = CreateQuickSetupPlaybook();
		var quickResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			quick,
			JsonOptions);
		quickResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await quickResponse.Content.ReadAsStringAsync());
		var quickCreated = await quickResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		quickCreated.ShouldNotBeNull();
		quickCreated.Status.ShouldBe(AdmissionCrmPlaybookStatus.Draft);
		quickCreated.IsFallback.ShouldBeTrue();
		quickCreated.RegistrationPath.ShouldBe(AdmissionCrmRegistrationPath.ExamWaived);
		quickCreated.ExamDefinitions.ShouldBeEmpty();

		var advanced = CreateValidPlaybook(isFallback: false);
		var advancedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			advanced,
			JsonOptions);
		advancedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await advancedResponse.Content.ReadAsStringAsync());
		var advancedCreated = await advancedResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		advancedCreated.ShouldNotBeNull();
		advancedCreated.Status.ShouldBe(AdmissionCrmPlaybookStatus.Draft);
		advancedCreated.IsFallback.ShouldBeFalse();
		advancedCreated.Applicability.Campus.ShouldBe(advanced.Applicability.Campus);
		advancedCreated.ExamDefinitions.ShouldNotBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Publish_Resolve_And_Version_Playbook()
	{
		var input = CreateValidPlaybook(isFallback: false);
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			input,
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.Status.ShouldBe(AdmissionCrmPlaybookStatus.Draft);
		created.Version.ShouldBe(1);
		created.SeatPlans[0].RemainingCapacity.ShouldBe(24);
		created.SeatPlans[0].IsOverAllocated.ShouldBeFalse();

		var publishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{created.Id}/publish",
			content: null);
		publishResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());
		var published = await publishResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		published.ShouldNotBeNull();
		published.Status.ShouldBe(AdmissionCrmPlaybookStatus.Published);
		published.PublishedAt.ShouldNotBeNull();

		var resolveResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/playbooks/resolve",
			new ResolveAdmissionCrmPlaybookDto { Campus = input.Applicability.Campus },
			JsonOptions);
		resolveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resolveResponse.Content.ReadAsStringAsync());
		var resolution = await resolveResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookResolutionDto>(JsonOptions);
		resolution.ShouldNotBeNull();
		resolution.Playbook.Id.ShouldBe(created.Id);
		resolution.SpecificityScore.ShouldBe(1);
		resolution.Explanation.ShouldContain("most specific");

		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{created.Id}",
			CreateValidPlaybook(isFallback: false),
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await updateResponse.Content.ReadAsStringAsync());

		var newVersionResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{created.Id}/new-version",
			content: null);
		newVersionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await newVersionResponse.Content.ReadAsStringAsync());
		var newVersion = await newVersionResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		newVersion.ShouldNotBeNull();
		newVersion.Status.ShouldBe(AdmissionCrmPlaybookStatus.Draft);
		newVersion.Version.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Ambiguous_Published_Scope()
	{
		var firstInput = CreateValidPlaybook(isFallback: false);
		var first = await CreateAndPublishAsync(firstInput);
		first.Status.ShouldBe(AdmissionCrmPlaybookStatus.Published);

		var secondInput = CreateValidPlaybook(isFallback: false, name: "Second scoped playbook");
		secondInput.Applicability.Campus = firstInput.Applicability.Campus;
		var secondCreateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			secondInput,
			JsonOptions);
		secondCreateResponse.EnsureSuccessStatusCode();
		var second = await secondCreateResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		second.ShouldNotBeNull();

		var secondPublishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{second.Id}/publish",
			content: null);
		secondPublishResponse.StatusCode.ShouldBe(
			HttpStatusCode.BadRequest,
			await secondPublishResponse.Content.ReadAsStringAsync());
		(await secondPublishResponse.Content.ReadAsStringAsync()).ShouldContain(
			"AdmissionCrm:ConflictingPublishedPlaybook");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Circular_Stages_And_Invalid_Exam_Weights()
	{
		var circular = CreateValidPlaybook(isFallback: false);
		circular.Stages =
		[
			new AdmissionCrmPlaybookStageDto
			{
				Code = "START",
				DisplayName = "Start",
				ReportingMilestone = AdmissionInquiryStage.NewInquiry,
				Order = 1,
				AllowedNextStageCodes = ["REVIEW"]
			},
			new AdmissionCrmPlaybookStageDto
			{
				Code = "REVIEW",
				DisplayName = "Review",
				ReportingMilestone = AdmissionInquiryStage.FollowUp,
				Order = 2,
				AllowedNextStageCodes = ["START"]
			}
		];

		var circularResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			circular,
			JsonOptions);
		circularResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await circularResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:CircularPlaybookStages");

		var invalidWeights = CreateValidPlaybook(isFallback: false, name: "Invalid exam weights");
		invalidWeights.ExamDefinitions[0].Components[0].WeightPercentage = 90;
		var invalidWeightsResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			invalidWeights,
			JsonOptions);
		invalidWeightsResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await invalidWeightsResponse.Content.ReadAsStringAsync()).ShouldContain(
			"AdmissionCrm:ExamComponentWeightsMustTotalOneHundred");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Generate_Recommend_And_Preview_Configured_Exam()
	{
		var published = await CreateAndPublishAsync(CreateValidPlaybook(isFallback: false));
		var fromDate = DateTime.UtcNow.Date.AddDays(1);
		var toDate = fromDate.AddDays(7);
		var generateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{published.Id}/slots/generate",
			new GenerateAdmissionCrmPlaybookSlotsDto
			{
				SlotTemplateCode = "WEEKDAY-MORNING",
				FromDate = fromDate,
				ToDate = toDate,
				UtcOffsetMinutes = 0
			},
			JsonOptions);
		generateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await generateResponse.Content.ReadAsStringAsync());
		var generated = await generateResponse.Content.ReadFromJsonAsync<AdmissionCrmSlotGenerationResultDto>(JsonOptions);
		generated.ShouldNotBeNull();
		generated.Slots.Count.ShouldBeGreaterThan(0);
		generated.Slots.ShouldAllBe(x => x.RoomNo > 0 && x.Capacity == 30);

		var recommendResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{published.Id}/slots/recommend",
			new RecommendAdmissionCrmPlaybookSlotDto
			{
				AssignmentRuleCode = "AUTO-EXAM-READY"
			},
			JsonOptions);
		recommendResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await recommendResponse.Content.ReadAsStringAsync());
		var recommendation = await recommendResponse.Content.ReadFromJsonAsync<AdmissionCrmSlotRecommendationDto>(JsonOptions);
		recommendation.ShouldNotBeNull();
		recommendation.AvailableSeats.ShouldBe(30);
		recommendation.Strategy.ShouldBe(AdmissionCrmSlotAssignmentStrategy.EarliestAvailable);
		recommendation.Explanation.ShouldContain("AUTO-EXAM-READY");

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{published.Id}/exam-results/preview",
			new PreviewAdmissionCrmExamResultDto
			{
				ExamDefinitionCode = "GRADE1-ASSESSMENT",
				ComponentScores = new Dictionary<string, decimal>
				{
					["READINESS"] = 72
				}
			},
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content.ReadFromJsonAsync<AdmissionCrmExamResultPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Passed.ShouldBeTrue();
		preview.Outcome.ShouldBe("Pass");
		preview.WeightedPercentage.ShouldBe(72);
		preview.Components[0].PassedMinimum.ShouldBeTrue();
	}

	private async Task<AdmissionCrmPlaybookDto> CreateAndPublishAsync(CreateAdmissionCrmPlaybookDto input)
	{
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/playbooks",
			input,
			JsonOptions);
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		created.ShouldNotBeNull();

		var publishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/playbooks/{created.Id}/publish",
			content: null);
		publishResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());
		var published = await publishResponse.Content.ReadFromJsonAsync<AdmissionCrmPlaybookDto>(JsonOptions);
		published.ShouldNotBeNull();
		return published;
	}

	private static CreateAdmissionCrmPlaybookDto CreateValidPlaybook(
		bool isFallback,
		string? name = null)
	{
		var now = DateTime.UtcNow.Date;
		var roomNo = Random.Shared.Next(1000, 9999);
		return new CreateAdmissionCrmPlaybookDto
		{
			Name = name ?? $"Primary admissions playbook {Guid.NewGuid():N}",
			IsFallback = isFallback,
			RegistrationPath = AdmissionCrmRegistrationPath.RegistrationAfterExam,
			Applicability = isFallback
				? new AdmissionCrmPlaybookApplicabilityDto()
				: new AdmissionCrmPlaybookApplicabilityDto { Campus = $"Campus {Guid.NewGuid():N}" },
			Stages =
			[
				new AdmissionCrmPlaybookStageDto
				{
					Code = "EXAM_READY",
					DisplayName = "Exam ready",
					ReportingMilestone = AdmissionInquiryStage.ExamReady,
					Order = 1,
					IsRequired = true,
					AllowedNextStageCodes = ["ADMISSION_READY"]
				},
				new AdmissionCrmPlaybookStageDto
				{
					Code = "ADMISSION_READY",
					DisplayName = "Admission ready",
					ReportingMilestone = AdmissionInquiryStage.Eligible,
					Order = 2,
					IsRequired = true
				}
			],
			Rounds =
			[
				new AdmissionCrmAdmissionRoundDto
				{
					Code = "REGULAR",
					Name = "Regular intake",
					OpeningDate = now,
					ClosingDate = now.AddMonths(2),
					ExamWindowStart = now.AddDays(7),
					ExamWindowEnd = now.AddMonths(1),
					RegistrationDeadline = now.AddMonths(2),
					DecisionDeadline = now.AddMonths(2).AddDays(7)
				}
			],
			SeatPlans =
			[
				new AdmissionCrmSeatPlanDto
				{
					Code = "GRADE1-REGULAR",
					RoundCode = "REGULAR",
					DesiredClass = "Grade 1",
					PlannedSeats = 30,
					ApprovedCapacity = 30,
					OfferedSeats = 8,
					AcceptedSeats = 6,
					WaitlistedApplicants = 2
				}
			],
			ExamDefinitions =
			[
				new AdmissionCrmExamDefinitionDto
				{
					Code = "GRADE1-ASSESSMENT",
					Name = "Grade 1 readiness assessment",
					RoundCode = "REGULAR",
					DesiredClass = "Grade 1",
					DurationMinutes = 60,
					Components =
					[
						new AdmissionCrmExamComponentDto
						{
							Code = "READINESS",
							Name = "Readiness",
							ComponentType = AdmissionCrmAssessmentComponentType.ReadinessAssessment,
							MaximumMarks = 100,
							MinimumMarks = 40,
							WeightPercentage = 100,
							IsMandatory = true
						}
					],
					ResultRule = new AdmissionCrmExamResultRuleDto
					{
						AggregatePassingPercentage = 40,
						MeritCapacity = 30,
						WaitlistCapacity = 10,
						TieBreakerCodes = ["READINESS", "InquiryDate"]
					}
				}
			],
			SlotTemplates =
			[
				new AdmissionCrmSlotTemplateDto
				{
					Code = "WEEKDAY-MORNING",
					Name = "Weekday morning",
					ExamDefinitionCode = "GRADE1-ASSESSMENT",
					RoomOrHall = "Hall A",
					RoomNo = roomNo,
					Capacity = 30,
					ReportingTime = new TimeSpan(8, 30, 0),
					StartTime = new TimeSpan(9, 0, 0),
					EndTime = new TimeSpan(10, 0, 0),
					ValidFrom = now,
					ValidTo = now.AddMonths(2),
					DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Wednesday]
				}
			],
			AssignmentRules =
			[
				new AdmissionCrmSlotAssignmentRuleDto
				{
					Code = "AUTO-EXAM-READY",
					Name = "Auto assign when exam ready",
					Trigger = "ExamReady",
					Strategy = AdmissionCrmSlotAssignmentStrategy.EarliestAvailable,
					Priority = 1,
					SlotTemplateCode = "WEEKDAY-MORNING"
				}
			]
		};
	}

	private static CreateAdmissionCrmPlaybookDto CreateQuickSetupPlaybook()
	{
		return new CreateAdmissionCrmPlaybookDto
		{
			Name = $"Quick setup {Guid.NewGuid():N}",
			IsFallback = true,
			RegistrationPath = AdmissionCrmRegistrationPath.ExamWaived,
			Applicability = new AdmissionCrmPlaybookApplicabilityDto(),
			Stages =
			[
				new AdmissionCrmPlaybookStageDto
				{
					Code = "APPLICATION_REVIEW",
					DisplayName = "Application review",
					ReportingMilestone = AdmissionInquiryStage.FollowUp,
					Order = 10,
					IsRequired = true,
					IsParentVisible = true,
					SlaHours = 24,
					AllowedNextStageCodes = ["ADMISSION_READY"]
				},
				new AdmissionCrmPlaybookStageDto
				{
					Code = "ADMISSION_READY",
					DisplayName = "Admission ready",
					ReportingMilestone = AdmissionInquiryStage.Eligible,
					Order = 20,
					IsRequired = true,
					IsParentVisible = true,
					AllowedNextStageCodes = []
				}
			],
			Rounds = [],
			SeatPlans = [],
			ExamDefinitions = [],
			SlotTemplates = [],
			AssignmentRules = [],
			Notes = null
		};
	}
}
