using System;
using System.Text.Json.Nodes;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentOnboarding_Tests
{
	[Fact]
	public void Settings_recognize_an_idempotent_configuration_retry()
	{
		const string configuration = "{\"readinessAccess\":{\"requireDocuments\":true}}";
		var settings = new StudentOnboardingSettings(Guid.NewGuid(), Guid.NewGuid(), null,
			StudentOnboardingSettingsConsts.CurrentSchemaVersion, configuration);

		settings.MatchesConfiguration(StudentOnboardingSettingsConsts.CurrentSchemaVersion,
			$"  {configuration}  ").ShouldBeTrue();
		settings.MatchesConfiguration(StudentOnboardingSettingsConsts.CurrentSchemaVersion,
			"{\"readinessAccess\":{\"requireDocuments\":false}}").ShouldBeFalse();
		settings.MatchesConfiguration(StudentOnboardingSettingsConsts.CurrentSchemaVersion + 1,
			configuration).ShouldBeFalse();
	}

	[Theory]
	[InlineData("{SEQUENCE}{SEQUENCE}")]
	[InlineData("{SCHOOL_CODE}{SEQUENCE}")]
	[InlineData("GR-2026")]
	public void Configuration_rejects_invalid_GR_formats_before_runtime(string pattern)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(new
		{
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(),
			grNumber = new { configurations = new[] { new { formatPattern = pattern } } },
			rollNumber = new { configurations = Array.Empty<object>() },
			divisionChange = new { configurations = Array.Empty<object>() }
		});
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(json))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData("assignmentMode", "sometimes")]
	[InlineData("sequenceScope", "board")]
	[InlineData("resetPolicy", "monthly")]
	[InlineData("numberingScope", "school")]
	public void Configuration_rejects_unsupported_numbering_modes_before_runtime(string key, string value)
	{
		const string json = """
			{"readinessAccess":{},"onboardingProfiles":[],
			"grNumber":{"assignmentMode":"both","configurations":[{"id":"gr","formatPattern":"{SEQUENCE}",
			"sequenceScope":"school","resetPolicy":"continuous"}]},
			"rollNumber":{"configurations":[{"id":"roll","numberingScope":"class-division"}]},
			"divisionChange":{"configurations":[]}}
			""";
		var root = JsonNode.Parse(json)!;
		var target = key switch
		{
			"assignmentMode" => root["grNumber"]!,
			"numberingScope" => root["rollNumber"]!["configurations"]![0]!,
			_ => root["grNumber"]!["configurations"]![0]!
		};
		target[key] = value;
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Fact]
	public void Configuration_accepts_supported_numbering_modes()
	{
		const string json = """
			{"readinessAccess":{},"onboardingProfiles":[],
			"grNumber":{"assignmentMode":"manual","configurations":[{"id":"gr","formatPattern":"{SEQUENCE}",
			"sequenceScope":"academic-year","resetPolicy":"academic-year"}]},
			"rollNumber":{"configurations":[{"id":"roll","numberingScope":"class"}]},
			"divisionChange":{"configurations":[]}}
			""";
		_ = new OnboardingRuleConfiguration(json);
	}

	[Theory]
	[InlineData("readinessAccess", "studentAccount", "\"false\"")]
	[InlineData("profileRules", "guardianAccount", "null")]
	[InlineData("profileRules", "approvalMode", "\"sometimes\"")]
	[InlineData("profileRules", "approvalTriggers", "\"manual-identifier\"")]
	[InlineData("profileRules", "approvalTriggers", "[42]")]
	[InlineData("profileRules", "checks", "{}")]
	[InlineData("documentCheck", "required", "\"false\"")]
	[InlineData("grNumber", "manualEntryAllowed", "\"true\"")]
	[InlineData("grNumber", "maximumNumber", "\"100\"")]
	[InlineData("grNumber", "maximumNumber", "\"\"")]
	[InlineData("rollNumber", "requireApproval", "\"true\"")]
	[InlineData("divisionChange", "isActive", "\"false\"")]
	public void Configuration_rejects_malformed_operational_rules_before_they_can_weaken_readiness(
		string section, string key, string value)
	{
		const string json = """
			{"readinessAccess":{},"onboardingProfiles":[{"id":"fresh","admissionRoutes":["Fresh admission"],
			"applicability":{},"rules":{"checks":[{"key":"documents","required":true}]}}],
			"grNumber":{"configurations":[{"id":"gr","formatPattern":"{SEQUENCE}"}]},
			"rollNumber":{"configurations":[{"id":"roll"}]},
			"divisionChange":{"configurations":[{"id":"division"}]}}
			""";
		var root = JsonNode.Parse(json)!;
		var target = section switch
		{
			"profileRules" => root["onboardingProfiles"]![0]!["rules"]!,
			"documentCheck" => root["onboardingProfiles"]![0]!["rules"]!["checks"]![0]!,
			"readinessAccess" => root[section]!,
			_ => root[section]!["configurations"]![0]!
		};
		target[key] = JsonNode.Parse(value);
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("null")]
	[InlineData("100")]
	public void Configuration_accepts_an_omitted_null_or_numeric_GR_maximum(string? maximum)
	{
		const string json = """
			{"readinessAccess":{},"onboardingProfiles":[],
			"grNumber":{"configurations":[{"id":"gr","formatPattern":"{SEQUENCE}"}]},
			"rollNumber":{"configurations":[]},"divisionChange":{"configurations":[]}}
			""";
		var root = JsonNode.Parse(json)!;
		if (maximum != null)
			root["grNumber"]!["configurations"]![0]!["maximumNumber"] = JsonNode.Parse(maximum);
		Should.NotThrow(() => new OnboardingRuleConfiguration(root.ToJsonString()));
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("[\"Secondary\",\"secondary\"]")]
	[InlineData("[\" \" ]")]
	public void Configuration_rejects_invalid_onboarding_school_levels(string names)
	{
		using var document = System.Text.Json.JsonDocument.Parse(names);
		var json = System.Text.Json.JsonSerializer.Serialize(new
		{
			academicAvailability = new { schoolLevelNames = document.RootElement },
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(),
			grNumber = new { configurations = Array.Empty<object>() },
			rollNumber = new { configurations = Array.Empty<object>() },
			divisionChange = new { configurations = Array.Empty<object>() }
		});
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(json)
			.AvailableSchoolLevelNames).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Fact]
	public void Configuration_accepts_school_level_names_without_changing_academic_setup()
	{
		const string json = """
			{"academicAvailability":{"schoolLevelNames":["Secondary","Higher Secondary"]},
			"readinessAccess":{},"onboardingProfiles":[],
			"grNumber":{"configurations":[]},"rollNumber":{"configurations":[]},
			"divisionChange":{"configurations":[]}}
			""";
		new OnboardingRuleConfiguration(json).AvailableSchoolLevelNames
			.ShouldBe(new[] { "Secondary", "Higher Secondary" });
	}

	[Fact]
	public void Campus_targeting_matches_only_the_admission_offering_campus_and_preserves_old_rules()
	{
		var item = CreateCase(DateTime.UtcNow);
		var campusId = Guid.NewGuid();
		item.SetCampusId(campusId);
		using var selected = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
		{
			campusMode = "selected", campusIds = new[] { campusId.ToString() },
			boardMode = "all", mediumMode = "all", stageMode = "all",
			classMode = "all", divisionMode = "all"
		}));
		OnboardingRuleConfiguration.Matches(selected.RootElement, item).ShouldBeTrue();
		OnboardingRuleConfiguration.Score(selected.RootElement).ShouldBe(32);
		item.SetCampusId(Guid.NewGuid());
		OnboardingRuleConfiguration.Matches(selected.RootElement, item).ShouldBeFalse();
		item.SetCampusId(null);
		OnboardingRuleConfiguration.Matches(selected.RootElement, item).ShouldBeFalse();
		using var legacy = System.Text.Json.JsonDocument.Parse("""
			{"boardMode":"all","mediumMode":"all","stageMode":"all","classMode":"all","divisionMode":"all"}
			""");
		OnboardingRuleConfiguration.Matches(legacy.RootElement, item).ShouldBeTrue();
	}

	[Fact]
	public void Academic_path_matching_requires_the_selected_board_medium_level_and_standard()
	{
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var levelId = Guid.NewGuid();
		var standardId = Guid.NewGuid();
		using var selected = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
		{
			boardMode = "selected", boardIds = new[] { boardId },
			mediumMode = "selected", academicMediumIds = new[] { mediumId },
			stageMode = "selected", academicMediumStageIds = new[] { levelId },
			classMode = "selected", standardIds = new[] { standardId },
			divisionMode = "all"
		}));
		StudentOnboardingCase Case(Guid board, Guid medium, Guid level, Guid standard) => new(
			Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), board, medium, level, standard, $"path:{Guid.NewGuid():N}",
			DateTime.UtcNow, DateTime.UtcNow.AddHours(48));

		OnboardingRuleConfiguration.Matches(selected.RootElement,
			Case(boardId, mediumId, levelId, standardId)).ShouldBeTrue();
		OnboardingRuleConfiguration.Matches(selected.RootElement,
			Case(Guid.NewGuid(), mediumId, levelId, standardId)).ShouldBeFalse();
		OnboardingRuleConfiguration.Matches(selected.RootElement,
			Case(boardId, Guid.NewGuid(), levelId, standardId)).ShouldBeFalse();
		OnboardingRuleConfiguration.Matches(selected.RootElement,
			Case(boardId, mediumId, Guid.NewGuid(), standardId)).ShouldBeFalse();
		OnboardingRuleConfiguration.Matches(selected.RootElement,
			Case(boardId, mediumId, levelId, Guid.NewGuid())).ShouldBeFalse();
	}

	[Fact]
	public void Legacy_campus_recovery_does_not_write_a_snapshot_during_a_read()
	{
		var item = CreateCase(DateTime.UtcNow);
		var campusId = Guid.NewGuid();
		item.HasCampusSnapshot.ShouldBeFalse();
		item.UseLegacyCampusId(campusId);
		item.GetCampusId().ShouldBe(campusId);
		item.HasCampusSnapshot.ShouldBeFalse();
		item.ExtraProperties.ContainsKey("AdmissionCampusId").ShouldBeFalse();
		item.SetCampusId(null);
		item.GetCampusId().ShouldBeNull();
		item.HasCampusSnapshot.ShouldBeTrue();
	}

	[Fact]
	public void Campus_specific_readiness_and_numbering_win_over_all_campus_fallbacks()
	{
		var campusId = Guid.NewGuid();
		var applicability = new
		{
			boardMode = "all", mediumMode = "all", stageMode = "all",
			classMode = "all", divisionMode = "all"
		};
		var json = System.Text.Json.JsonSerializer.Serialize(new
		{
			readinessAccess = new { requireDocuments = false },
			onboardingProfiles = new object[]
			{
				new { id = "all-campus", admissionRoutes = new[] { "Fresh admission" },
					applicability, rules = new { requireDocuments = false } },
				new { id = "campus-only", admissionRoutes = new[] { "Fresh admission" },
					applicability = new { campusMode = "selected", campusIds = new[] { campusId },
						applicability.boardMode, applicability.mediumMode, applicability.stageMode,
						applicability.classMode, applicability.divisionMode },
					rules = new { requireDocuments = true } }
			},
			grNumber = new { configurations = new object[]
			{
				new { id = "all-campus-gr", isActive = true, applicability.boardMode,
					applicability.mediumMode, applicability.stageMode, applicability.classMode,
					applicability.divisionMode },
				new { id = "campus-gr", isActive = true, campusMode = "selected",
					campusIds = new[] { campusId }, applicability.boardMode,
					applicability.mediumMode, applicability.stageMode, applicability.classMode,
					applicability.divisionMode }
			} },
			rollNumber = new { configurations = Array.Empty<object>() },
			divisionChange = new { configurations = Array.Empty<object>() }
		});
		var config = new OnboardingRuleConfiguration(json);
		var item = CreateCase(DateTime.UtcNow);
		item.SetCampusId(campusId);
		config.ResolveProfile(item)!.Value.GetProperty("id").GetString().ShouldBe("campus-only");
		config.ResolveRule("grNumber", item, DateTime.UtcNow)!.Value.GetProperty("id")
			.GetString().ShouldBe("campus-gr");

		item.SetCampusId(null);
		config.ResolveProfile(item)!.Value.GetProperty("id").GetString().ShouldBe("all-campus");
		config.ResolveRule("grNumber", item, DateTime.UtcNow)!.Value.GetProperty("id")
			.GetString().ShouldBe("all-campus-gr");
	}

	[Fact]
	public void School_defined_admission_route_is_valid_but_blank_or_untrimmed_routes_are_not()
	{
		const string json = """
			{"readinessAccess":{},"onboardingProfiles":[{"id":"special","admissionRoutes":["Scholarship intake"],
			"applicability":{"boardMode":"all","mediumMode":"all","stageMode":"all",
			"classMode":"all","divisionMode":"all"},"rules":{}}],"grNumber":{"configurations":[]},
			"rollNumber":{"configurations":[]},"divisionChange":{"configurations":[]}}
			""";
		var configuration = new OnboardingRuleConfiguration(json);
		configuration.FindAdmissionRoute(" scholarship INTAKE ")
			.ShouldBe("Scholarship intake");
		var item = CreateCase(DateTime.UtcNow);
		item.SetAdmissionRoute("Scholarship intake");
		item.GetAdmissionRoute().ShouldBe("Scholarship intake");
		configuration.ResolveProfile(item)!.Value.GetProperty("id").GetString().ShouldBe("special");
		OnboardingRuleConfiguration.EnsureAdmissionRouteCovered(item, configuration);
		Should.Throw<BusinessException>(() => OnboardingRuleConfiguration.EnsureAdmissionRouteCovered(item, null))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		var otherCampus = Guid.NewGuid();
		var campusOnly = new OnboardingRuleConfiguration(json.Replace(
			"\"boardMode\":\"all\"",
			$"\"campusMode\":\"selected\",\"campusIds\":[\"{otherCampus}\"],\"boardMode\":\"all\""));
		item.SetCampusId(Guid.NewGuid());
		Should.Throw<BusinessException>(() => OnboardingRuleConfiguration.EnsureAdmissionRouteCovered(item, campusOnly))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		item.SetAdmissionRoute("Fresh admission");
		OnboardingRuleConfiguration.EnsureAdmissionRouteCovered(item, null);
		Should.Throw<BusinessException>(() => item.SetAdmissionRoute(" "))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		Should.Throw<BusinessException>(() => item.SetAdmissionRoute(" Scholarship intake "))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		Should.Throw<BusinessException>(() => item.SetAdmissionRoute("Scholarship\nintake"))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Fact]
	public void Enrollment_does_not_complete_provisioning_and_cannot_be_cancelled_or_replaced()
	{
		var now = DateTime.UtcNow;
		var item = CreateCase(now);
		item.RecordPlacement(Guid.NewGuid(), Guid.NewGuid(), now);
		item.RecordIdentifier(Guid.NewGuid(), 1, now);
		item.MarkReady(now);
		var enrollmentId = Guid.NewGuid();
		item.BeginProvisioning(enrollmentId);
		item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
		item.CompletedAt.ShouldBeNull();
		Should.Throw<BusinessException>(() => item.Cancel("Cannot undo an enrollment", now));
		Should.Throw<BusinessException>(() => item.RecordIdentifier(Guid.NewGuid(), 2, now));
		item.RequireAttention("First installment not settled");
		item.StudentEnrollmentId.ShouldBe(enrollmentId);
		item.CompleteProvisioning(now.AddMinutes(10));
		item.Status.ShouldBe(StudentOnboardingStatus.Completed);
		item.BeginProvisioning(enrollmentId);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed);
	}

	[Fact]
	public void Editing_a_GR_rule_never_rewinds_its_counter()
	{
		var series = new StudentIdentifierSeries(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			StudentIdentifierType.GrNumber, "TEST", "Test", "GR-", "", 4, 10, null, DateTime.UnixEpoch, null);
		series.Allocate(DateTime.UtcNow).Value.ShouldBe("GR-0010");
		series.ApplyRule("Renamed", "GR-", "", 4, 1, null);
		series.Allocate(DateTime.UtcNow).Value.ShouldBe("GR-0011");
	}
	[Fact]
	public void Case_tracks_SLA_placement_identifiers_and_terminal_activation()
	{
		var now = DateTime.UtcNow;
		var onboardingCase = CreateCase(now);
		var divisionId = Guid.NewGuid();
		var reservationId = Guid.NewGuid();
		var enrollmentId = Guid.NewGuid();

		onboardingCase.DueAt.ShouldBe(now.AddHours(48), TimeSpan.FromMilliseconds(1));
		onboardingCase.RecordPlacement(divisionId, reservationId, now.AddMinutes(1));
		onboardingCase.RecordIdentifier(Guid.NewGuid(), 17, now.AddMinutes(2));
		onboardingCase.MarkReady(now.AddMinutes(3));
		onboardingCase.Complete(enrollmentId, now.AddMinutes(4));

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Completed);
		onboardingCase.StudentEnrollmentId.ShouldBe(enrollmentId);
		onboardingCase.DivisionId.ShouldBe(divisionId);
		onboardingCase.RollNumber.ShouldBe(17);
		Should.Throw<BusinessException>(() => onboardingCase.AssignOwner(Guid.NewGuid()));
	}

	[Fact]
	public void Cancellation_is_terminal_and_keeps_identity_reference()
	{
		var onboardingCase = CreateCase(DateTime.UtcNow);
		var studentId = onboardingCase.StudentId;

		onboardingCase.Cancel("Family deferred admission", DateTime.UtcNow.AddMinutes(1));

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Cancelled);
		onboardingCase.StudentId.ShouldBe(studentId);
		onboardingCase.CancellationReason.ShouldBe("Family deferred admission");
		Should.Throw<BusinessException>(() => onboardingCase.RecordProgress(DateTime.UtcNow));
	}

	[Fact]
	public void Activation_failure_requires_attention_and_can_resume_after_staff_progress()
	{
		var onboardingCase = CreateCase(DateTime.UtcNow);

		onboardingCase.RequireAttention("Fee clearance could not be verified.");

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
		onboardingCase.LastError.ShouldBe("Fee clearance could not be verified.");

		onboardingCase.RecordProgress(DateTime.UtcNow.AddMinutes(1));
		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.InProgress);
		onboardingCase.LastError.ShouldBeNull();
	}

	[Fact]
	public void Historical_import_is_completed_without_fabricating_guardian_verification()
	{
		var completedAt = DateTime.UtcNow.AddYears(-1);
		var onboardingCase = CreateHistoricalCase(completedAt);
		var enrollmentId = Guid.NewGuid();
		var divisionId = Guid.NewGuid();

		onboardingCase.CompleteHistoricalImport(enrollmentId, divisionId, 23, completedAt);

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Completed);
		onboardingCase.IsHistoricalBackfill.ShouldBeTrue();
		onboardingCase.GuardianContactVerifiedAt.ShouldBeNull();
		onboardingCase.StudentEnrollmentId.ShouldBe(enrollmentId);
		onboardingCase.DivisionId.ShouldBe(divisionId);
		onboardingCase.RollNumber.ShouldBe(23);
		onboardingCase.DueAt.ShouldBe(completedAt, TimeSpan.FromMilliseconds(1));
	}

	[Fact]
	public void Enrollment_historical_link_is_idempotent_and_rejects_a_conflicting_case()
	{
		var onboardingCaseId = Guid.NewGuid();
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid());

		enrollment.LinkOnboardingCase(onboardingCaseId);
		enrollment.LinkOnboardingCase(onboardingCaseId);

		enrollment.OnboardingCaseId.ShouldBe(onboardingCaseId);
		Should.Throw<BusinessException>(() => enrollment.LinkOnboardingCase(Guid.NewGuid()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}

	[Fact]
	public void Seat_reservation_cannot_commit_after_expiry()
	{
		var now = DateTime.UtcNow;
		var reservation = new StudentSeatReservation(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 1, now, now.AddMinutes(30));

		reservation.IsLive(now.AddMinutes(29)).ShouldBeTrue();
		Should.Throw<BusinessException>(() => reservation.Commit(now.AddMinutes(31)))
			.Code.ShouldBe(StudentOnboardingErrorCodes.SeatReservationExpired);
		reservation.Expire(now.AddMinutes(31));
		reservation.Status.ShouldBe(StudentSeatReservationStatus.Expired);
	}

	[Fact]
	public void Identifier_series_formats_values_and_honours_the_maximum()
	{
		var now = DateTime.UtcNow;
		var series = new StudentIdentifierSeries(Guid.NewGuid(), null, Guid.NewGuid(), null,
			StudentIdentifierType.GrNumber, "GR-2027", "General Register 2027-28", "GR/27/", "", 5,
			9, 10, now.AddDays(-1), now.AddDays(1));

		series.Allocate(now).ShouldBe((9L, "GR/27/00009"));
		series.Allocate(now).ShouldBe((10L, "GR/27/00010"));
		Should.Throw<BusinessException>(() => series.Allocate(now))
			.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
	}

	[Fact]
	public void Identifier_series_rejects_manual_use_outside_its_effective_window()
	{
		var now = DateTime.UtcNow;
		var series = new StudentIdentifierSeries(Guid.NewGuid(), null, Guid.NewGuid(), null,
			StudentIdentifierType.GrNumber, "GR-FUTURE", "Future series", "GR/", "", 5,
			1, null, now.AddDays(1), now.AddDays(2));

		Should.Throw<BusinessException>(() => series.EnsureAvailable(now))
			.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
	}

	[Fact]
	public void Division_change_clears_roll_but_keeps_reserved_permanent_identifier()
	{
		var now = DateTime.UtcNow;
		var onboardingCase = CreateCase(now);
		var allocationId = Guid.NewGuid();
		onboardingCase.RecordPlacement(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
		onboardingCase.RecordIdentifier(allocationId, 17, now.AddMinutes(2));

		onboardingCase.ClearRollNumberForPlacementChange(now.AddMinutes(3));

		onboardingCase.RollNumber.ShouldBeNull();
		onboardingCase.IdentifierAllocationId.ShouldBe(allocationId);
		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.InProgress);
	}

	[Fact]
	public void Waived_step_records_actor_reason_and_completion()
	{
		var actorId = Guid.NewGuid();
		var step = new StudentOnboardingStep(Guid.NewGuid(), null, Guid.NewGuid(),
			StudentOnboardingStepKeys.MedicalAndPreviousSchool, "Medical", 5, true);

		step.Waive(actorId, DateTime.UtcNow, "Not required for this age group");

		step.Status.ShouldBe(StudentOnboardingStepStatus.Waived);
		step.CompletedByUserId.ShouldBe(actorId);
		step.WaiverReason.ShouldBe("Not required for this age group");
		step.CompletedAt.ShouldNotBeNull();
	}

	private static StudentOnboardingCase CreateCase(DateTime now) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		$"finalize:{Guid.NewGuid():N}", now, now.AddHours(48));

	private static StudentOnboardingCase CreateHistoricalCase(DateTime completedAt) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		$"historical:{Guid.NewGuid():N}", null, completedAt.AddHours(1));
}
