using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_fresh_admission_returns_serialize_same_and_distinct_keys_without_duplicate_journeys()
	{
		Client.Timeout = TimeSpan.FromMinutes(4);
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync();
		await InTenantAsync(async services =>
		{
			foreach (var studentId in fixture.Students)
				await services.GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new(studentId, TenantId,
					new DateTimeOffset(2016, 1, 12, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female, null, "PG-RETURN-" + studentId.ToString("N"), null, null), true);
			return true;
		});
		using var reviewer = CreateClientAs(fixture.Reviewer);
		reviewer.Timeout = TimeSpan.FromMinutes(4);
		reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		foreach (var studentId in fixture.Students)
		{
			var legacy = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{studentId}/legacy-overrides",
				new RequestStudentLegacyOverrideDto { Reason = "Synthetic historical register for Admission return concurrency" });
			await PostResultAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
				new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent historical review" });
		}
		var policy = StudentRecordsPolicy.Starter();
		policy.Changes["Status"].Mode = "Direct";
		policy.Transitions.RosterRemovalTiming = "EffectiveDate";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish",
			new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow);
		foreach (var studentId in fixture.Students)
		{
			var exit = new StudentRecordTransitionBatchInput
			{
				AcademicYearId = fixture.SourceYear, IdempotencyKey = "pg-admission-exit-" + studentId.ToString("N"),
				Items = [new() { StudentId = studentId, Outcome = "Withdraw", EffectiveDate = today, Reason = "Reviewed temporary withdrawal" }]
			};
			var preview = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", exit)).Items.Single();
			preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Code)));
			exit.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp;
			exit.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
			(await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", exit)).Items.Single().Status.ShouldBe("Applied");
		}
		foreach (var mode in new[] { AdmissionEntryMode.Inquiry, AdmissionEntryMode.DirectRegistration })
		{
			var studentId = fixture.Students[mode == AdmissionEntryMode.Inquiry ? 0 : 1];
			var offering = await SeedAdmissionReturnConcurrencyOfferingAsync(fixture, mode);
			using var availabilityResponse = await Client.GetAsync($"/api/student/enrollment/admissions/offerings/{offering.Id}/availability");
			availabilityResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await availabilityResponse.Content.ReadAsStringAsync());
			var availability = (await availabilityResponse.Content.ReadFromJsonAsync<Result<AdmissionOfferingAvailabilityDto>>(JsonOptions))!.Data!;
			availability.IsAvailable.ShouldBeTrue(await availabilityResponse.Content.ReadAsStringAsync());
			var before = await ReadAdmissionReturnSourceRowsAsync(studentId);
			var request = new StudentRecordReturnInput
			{
				StudentId = studentId, Route = "FreshApplication", TargetAcademicYearId = fixture.SourceYear,
				AdmissionOfferingId = offering.Id, EffectiveDate = today, Reason = "Reviewed Admission return",
				IdempotencyKey = "pg-admission-return-" + studentId.ToString("N"),
				Registration = new() { GuardianFirstName = "Provider", GuardianLastName = "Guardian", GuardianMobile = mode == AdmissionEntryMode.Inquiry ? "9876543210" : "9876543211" },
				Inquiry = mode == AdmissionEntryMode.Inquiry ? new() { Source = AdmissionInquirySource.WalkIn, SourceDetail = "Returning family visit" } : null
			};
			var otherRequest = JsonSerializer.Deserialize<StudentRecordReturnInput>(JsonSerializer.Serialize(request))!;
			if (mode == AdmissionEntryMode.DirectRegistration) otherRequest.IdempotencyKey += "-other";
			// Synchronize before the parent batch's unique key can block the competing request.
			factory.Barrier.Arm("INSERT INTO \"AppStudentRecordTransitionBatches\"");
			var pending = new[] { request, otherRequest }.Select(x => Client.PostAsJsonAsync(Transitions + "/returns", x, JsonOptions)).ToArray();
			try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
			catch (TimeoutException) { }
			finally { factory.Barrier.Release(); }
			var responses = await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(60));
			try
			{
				if (factory.Barrier.Connections.Count != 2)
				{
					foreach (var response in responses)
						output.WriteLine($"{mode} before barrier: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
					factory.Barrier.Connections.Count.ShouldBe(2, "Both requests must reach real serializable writes; inspect the recorded HTTP responses.");
				}
				factory.Barrier.Connections.Count.ShouldBe(2);
				factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "The conflict must arise naturally from PostgreSQL.");
				responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
				var winningIndex = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.OK);
				var rejected = responses[1 - winningIndex];
				rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
				(await rejected.Content.ReadAsStringAsync()).ShouldContain("Students:Records:ConcurrencyConflict");
				var accepted = (await responses[winningIndex].Content.ReadFromJsonAsync<Result<StudentRecordReturnDto>>(JsonOptions))!.Data!;
				accepted.Status.ShouldBe("AwaitingAdmission");
				accepted.TransitionId.ShouldNotBeNull();
				accepted.AdmissionInquiryId.HasValue.ShouldBe(mode == AdmissionEntryMode.Inquiry);
				accepted.AdmissionApplicationId.HasValue.ShouldBe(mode == AdmissionEntryMode.DirectRegistration);
				var saved = await ReadAdmissionReturnSourceRowsAsync(studentId);
				AssertAdmissionReturnSourcePreserved(before.Student, saved.Student);
				AssertAdmissionReturnSourcePreserved(before.Enrollment, saved.Enrollment);
				saved.Detail.ShouldBe(before.Detail);
				var winningRequest = winningIndex == 0 ? request : otherRequest;
				var replay = await PostResultAsync<StudentRecordReturnDto>(Client, Transitions + "/returns", winningRequest);
				replay.TransitionId.ShouldBe(accepted.TransitionId);
				replay.AdmissionInquiryId.ShouldBe(accepted.AdmissionInquiryId);
				replay.AdmissionApplicationId.ShouldBe(accepted.AdmissionApplicationId);
				await InTenantAsync(async services =>
				{
					(await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().CountAsync(x => x.StudentId == studentId && x.Outcome == "Return")).ShouldBe(1);
					var handoffs = await services.GetRequiredService<IRepository<StudentRecordReturnHandoff, Guid>>().GetListAsync(x => x.StudentId == studentId);
					handoffs.Count.ShouldBe(1);
					handoffs.Single().TransitionId.ShouldBe(accepted.TransitionId);
					var keys = new[] { request.IdempotencyKey, otherRequest.IdempotencyKey };
					(await services.GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().CountAsync(x => Enumerable.Contains(keys, x.IdempotencyKey))).ShouldBe(1);
					(await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().CountAsync(x => x.ExistingStudentId == studentId)).ShouldBe(1);
					(await services.GetRequiredService<IRepository<AdmissionInquiry, Guid>>().CountAsync(x => x.AdmissionOfferingId == offering.Id)).ShouldBe(mode == AdmissionEntryMode.Inquiry ? 1 : 0);
					(await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().CountAsync(x => x.AdmissionOfferingId == offering.Id)).ShouldBe(mode == AdmissionEntryMode.DirectRegistration ? 1 : 0);
					(await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == accepted.TransitionId && x.Action == "Submitted")).ShouldBe(1);
					(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(x => x.StudentId == studentId)).ShouldBe(1);
					return true;
				});
				(await ReadAdmissionReturnSourceRowsAsync(studentId)).ShouldBe(saved);
				if (mode == AdmissionEntryMode.DirectRegistration)
				{
					using var retry = await Client.PostAsJsonAsync(Transitions + "/returns", winningIndex == 0 ? otherRequest : request, JsonOptions);
					retry.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await retry.Content.ReadAsStringAsync());
					(await retry.Content.ReadAsStringAsync()).ShouldContain("Students:Records:InvalidChangeState");
					(await ReadAdmissionReturnSourceRowsAsync(studentId)).ShouldBe(saved);
				}
				output.WriteLine($"{mode}: natural PostgreSQL 40001 on distinct connections; one return/handoff/applicant/owner journey, preserved source business columns, expected ABP relationship revisions, and unchanged full rows on replay.");
			}
			finally { foreach (var response in responses) response.Dispose(); }
		}
	}

	private static void AssertAdmissionReturnSourcePreserved(string before, string after)
	{
		var original = JsonNode.Parse(before)!.AsObject();
		var saved = JsonNode.Parse(after)!.AsObject();
		// ABP 9 audits tracked FK principals when a dependent transition is added.
		// Check those revisions explicitly; every other column, including actor/history, stays unchanged.
		saved["ConcurrencyStamp"]!.GetValue<string>().ShouldNotBe(original["ConcurrencyStamp"]!.GetValue<string>());
		DateTimeOffset.Parse(saved["LastModificationTime"]!.GetValue<string>())
			.ShouldBeGreaterThan(DateTimeOffset.Parse(original["LastModificationTime"]!.GetValue<string>()));
		foreach (var field in new[] { "ConcurrencyStamp", "LastModificationTime" })
		{
			original.Remove(field);
			saved.Remove(field);
		}
		JsonNode.DeepEquals(saved, original).ShouldBeTrue("Admission return must preserve every source column except the verified ABP relationship audit revisions.");
	}

	private async Task<(string Student, string Enrollment, string Detail)> ReadAdmissionReturnSourceRowsAsync(Guid studentId)
	{
		await using var connection = new NpgsqlConnection(factory.ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("""
			SELECT to_jsonb(student)::text, to_jsonb(enrollment)::text, to_jsonb(detail)::text
			FROM "AppStudents" student
			JOIN "AppStudentEnrollments" enrollment ON enrollment."StudentId" = student."Id" AND enrollment."TenantId" = student."TenantId"
			JOIN "AppStudentAdditionalDetails" detail ON detail."Id" = student."Id" AND detail."TenantId" = student."TenantId"
			WHERE student."Id" = @student AND student."TenantId" = @tenant
			""", connection);
		command.Parameters.AddWithValue("student", studentId);
		command.Parameters.AddWithValue("tenant", TenantId);
		await using var reader = await command.ExecuteReaderAsync();
		(await reader.ReadAsync()).ShouldBeTrue();
		var result = (reader.GetString(0), reader.GetString(1), reader.GetString(2));
		(await reader.ReadAsync()).ShouldBeFalse();
		return result;
	}

	private async Task<AdmissionOffering> SeedAdmissionReturnConcurrencyOfferingAsync(Fixture fixture, AdmissionEntryMode mode) => await InTenantAsync(async services =>
	{
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow);
		var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, "Provider return " + mode, "PG_RETURN_" + mode, fixture.SourceYear, today.AddDays(-5), today.AddDays(30));
		cycle.Publish(Guid.Parse(ShikshaMitra.Infrastructure.Auth.TestAuthHandler.TestUserId), DateTime.UtcNow);
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
		var forms = services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
		var inquiryForm = new AdmissionFormDefinition(Guid.NewGuid(), TenantId, "Return inquiry " + mode, "RETURN_INQUIRY_" + mode, AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
		var registrationForm = new AdmissionFormDefinition(Guid.NewGuid(), TenantId, "Return registration " + mode, "RETURN_REG_" + mode, AdmissionFormLayoutType.SinglePage);
		foreach (var form in new[] { inquiryForm, registrationForm })
		{
			form.Publish(null);
			await forms.InsertAsync(form, true);
			var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "DETAILS", "Details", 0, null);
			await services.GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(section, true);
			await services.GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), form.Id, section.Id,
				"NOTES", "Additional notes", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 0, null, null, null, null, null), true);
		}
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TenantId, "Provider return " + mode, "PG_RETURN_" + mode);
		workflow.Publish(null);
		await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		var formStage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "FORM", "Registration", AdmissionWorkflowStageType.Form, 0, true, false, null,
			JsonSerializer.Serialize(new { formDefinitionId = registrationForm.Id }), isEntryStage: true);
		var done = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "DONE", "Ready for review", AdmissionWorkflowStageType.Custom, 1, true, false, null, null, isTerminalStage: true);
		var stages = services.GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
		await stages.InsertAsync(formStage, true); await stages.InsertAsync(done, true);
		await services.GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), workflow.Id, formStage.Id, done.Id, 1, isDefault: true), true);
		var offering = new AdmissionOffering(Guid.NewGuid(), TenantId, cycle.Id, null, factory.SeededBoardId, factory.SeededMediumId,
			factory.SeededMediumStageId, factory.SeededStandardId, "PG_RETURN_" + mode, true, null, null, entryMode: mode);
		offering.ConfigureReferences(workflow.Id, mode == AdmissionEntryMode.Inquiry ? inquiryForm.Id : null, registrationForm.Id);
		await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
		return offering;
	});
}
