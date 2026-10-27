using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task One_click_credentials_postgres_outbox_failure_rolls_back_flushed_account_and_same_stamp_retry_persists_once()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedCredentialsPostgresCaseAsync();
		var url = $"/api/student/onboarding/{fixture.CaseId}/credentials";
		var before = await GetCredentialsPostgresResultAsync<StudentCredentialsWorkspaceDto>(url);
		var owner = before.Recipients.Single(recipient => recipient.Relationship == "father");
		owner.Email.ShouldBe(fixture.Email); owner.CanSend.ShouldBeTrue(owner.BlockedReason);
		var command = new SendStudentCredentialsDto { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
			ExpectedEmail = owner.Email!, ConcurrencyStamp = before.ConcurrencyStamp };
		var settingsBefore = await CredentialsPostgresSettingsSnapshotAsync();
		var rulesBefore = await InTenantAsync(async services =>
			(await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(fixture.CaseId))
				.ExtraProperties["ReadinessRulesSnapshot"].ToString());
		var trigger = "credentials_gate_" + Guid.NewGuid().ToString("N");
		factory.Barrier.ProviderFailures.Clear();
		try
		{
			await ConfigureCredentialsOutboxFailureAsync(fixture.CaseId, trigger, install: true);
			using var failed = await Client.PostAsJsonAsync(url, command, JsonOptions);
			failed.IsSuccessStatusCode.ShouldBeFalse();
			// This code is raised only after the trigger observes the flushed identity,
			// link, acknowledged handoff and completed case in the command's transaction.
			factory.Barrier.ProviderFailures.Keys.ShouldContain("23514");
			factory.Barrier.ProviderFailures.Keys.ShouldNotContain("P0002");
			await InTenantAsync(async services =>
			{
				(await services.GetRequiredService<IRepository<IdentityUser, Guid>>().CountAsync(user => user.Email == fixture.Email)).ShouldBe(0);
				(await services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(link => link.StudentId == fixture.StudentId)).ShouldBe(0);
				(await services.GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == fixture.CaseId)).ShouldBe(0);
				var handoff = await services.GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().GetAsync(fixture.HandoffId);
				handoff.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Pending); handoff.AcknowledgedAt.ShouldBeNull();
				var item = await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(fixture.CaseId);
				item.ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp); item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
				item.CompletedAt.ShouldBeNull(); item.StudentEnrollmentId.ShouldBe(fixture.EnrollmentId);
				(await services.GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>().CountAsync(row => row.OnboardingCaseId == fixture.CaseId)).ShouldBe(0);
				return true;
			});
		}
		finally { await ConfigureCredentialsOutboxFailureAsync(fixture.CaseId, trigger, install: false); }

		var sent = await PostResultAsync<StudentCredentialsWorkspaceDto>(Client, url, command);
		var delivery = sent.Recipients.Single(recipient => recipient.Relationship == "father").Delivery!;
		delivery.Email.ShouldBe(fixture.Email); delivery.Status.ShouldBe("Pending");
		var replay = await PostResultAsync<StudentCredentialsWorkspaceDto>(Client, url, command);
		replay.ConcurrencyStamp.ShouldBe(sent.ConcurrencyStamp);
		replay.Recipients.Single(recipient => recipient.Relationship == "father").Delivery!.Id.ShouldBe(delivery.Id);
		await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
			(await users.CountAsync(user => user.Email == fixture.Email)).ShouldBe(1);
			var user = await users.GetAsync(user => user.Email == fixture.Email);
			user.PasswordHash.ShouldBeNull(); user.EmailConfirmed.ShouldBeFalse();
			(await services.GetRequiredService<IdentityUserManager>().GetRolesAsync(user)).ShouldBe(["Parent"]);
			var links = await services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetListAsync(link => link.StudentId == fixture.StudentId);
			links.Count.ShouldBe(1); var link = links.Single();
			link.UserId.ShouldBe(user.Id); link.Relationship.ShouldBe("father");
			link.VerifiedByUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			var rows = await services.GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetListAsync(row => row.CaseId == fixture.CaseId);
			rows.Count.ShouldBe(1); var row = rows.Single();
			row.Id.ShouldBe(delivery.Id!.Value); row.UserId.ShouldBe(user.Id); row.LinkId.ShouldBe(link.Id);
			row.Email.ShouldBe(fixture.Email); row.AccountType.ShouldBe("guardian"); row.Status.ShouldBe("Pending");
			row.RequiresSavedOwnerValidation().ShouldBeTrue(); row.EncryptedInitialPassword.ShouldBeNull(); row.AttemptCount.ShouldBe(0);
			row.RequestedByUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			(await services.GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().GetAsync(fixture.HandoffId))
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			var item = await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(fixture.CaseId);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.ExtraProperties["ReadinessRulesSnapshot"].ToString().ShouldBe(rulesBefore);
			(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(enrollment => enrollment.StudentId == fixture.StudentId)).ShouldBe(1);
			var events = await services.GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>().GetListAsync(row => row.OnboardingCaseId == fixture.CaseId);
			events.Count(row => row.EventType == "PortalAccountLinked").ShouldBe(1);
			events.Count(row => row.EventType == "CredentialEmailRequested").ShouldBe(1);
			return true;
		});
		(await CredentialsPostgresSettingsSnapshotAsync()).ShouldBe(settingsBefore);
		var current = await GetCredentialsPostgresResultAsync<StudentOnboardingCaseDto>($"/api/student/onboarding/{fixture.CaseId}");
		current.Status.ShouldBe(StudentOnboardingStatus.Completed); current.CredentialsPending.ShouldBeTrue();
		var remaining = await GetOnboardingWorklistPageAsync(Client,
			$"/api/student/onboarding?academicYearId={fixture.YearId}&pendingTask=credentials&status={(int)StudentOnboardingStatus.Completed}");
		remaining.TotalCount.ShouldBe(1); remaining.Items.Single().Id.ShouldBe(fixture.CaseId);
		remaining.Items.Single().CredentialsPending.ShouldBeTrue();
		output.WriteLine("Actual PostgreSQL HTTP atomicity: outbox rejection rolled back already-flushed access writes; same-stamp retry persisted one passwordless account, link and manual delivery intent. Completed case remains in Credentials Remaining until delivery. No SMTP worker ran.");
	}

	private async Task<T> GetCredentialsPostgresResultAsync<T>(string url)
	{
		using var response = await Client.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<Result<T>>(JsonOptions))!.Data!;
	}

	private Task<string> CredentialsPostgresSettingsSnapshotAsync() => InTenantAsync(async services =>
		JsonSerializer.Serialize((await services.GetRequiredService<IRepository<StudentOnboardingSettings, Guid>>().GetListAsync())
			.OrderBy(settings => settings.Id).Select(settings => new { settings.Id, settings.AcademicYearId,
				settings.ConfigurationJson, settings.ConcurrencyStamp })));

	// This is an activated operational fixture, not a full Admissions/Finance journey.
	// The requirements owner captures real applicable documents; no waiver, payment or
	// credential transport is fabricated to prepare the account command's prerequisites.
	private Task<CredentialsPostgresFixture> SeedCredentialsPostgresCaseAsync() => InTenantAsync(async services =>
	{
		var key = "CRED-PG-" + Guid.NewGuid().ToString("N")[..16];
		var sourceYear = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(factory.SeededAcademicYearId);
		var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync(key, sourceYear.StartDate.AddYears(20), sourceYear.EndDate.AddYears(20));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Credentials PG English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Credentials PG Secondary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Credentials PG Class 9", 9, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "Credentials PG A", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var configuration = JsonSerializer.Serialize(new
		{
			readinessAccess = new { studentAccount = false, guardianAccount = true,
				sendStudentCredentialEmail = false, sendGuardianCredentialEmail = false,
				credentialSendingTime = "completion", credentialProvisioningMode = "manual" },
			onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() },
			rollNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() }
		});
		await services.GetRequiredService<OnboardingConfigurationManager>().ValidateAsync(configuration, year.Id);
		await services.GetRequiredService<IRepository<StudentOnboardingSettings, Guid>>().InsertAsync(new StudentOnboardingSettings(
			Guid.NewGuid(), TenantId, year.Id, StudentOnboardingSettingsConsts.CurrentSchemaVersion, configuration), true);
		var email = $"father-{Guid.NewGuid():N}@example.test";
		var student = new Student(Guid.NewGuid(), TenantId, "Credentials", null, "Provider", year.Id, board.Id, medium.Id, stage.Id,
			standard.Id, division.Id, rollNo: 1, status: StudentLifeCycleStatusEnum.Enrolled);
		student.AssignGrNumber(key);
		student.UpdateFamilyInfo(new ParentGuardianDetails("Saved Father", null, null, email, "9000000002",
			null, null, null, null, null, null, null, null), null, null, null);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new StudentAdditionalDetail(
			student.Id, TenantId, new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, null, key, null, null), true);
		student = await services.GetRequiredService<IStudentRepository>().GetWithDetailsAsync(student.Id)
			?? throw new InvalidOperationException("The synthetic activated Student fixture is unavailable.");
		var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, key, key, year.Id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TenantId, key, key);
		await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TenantId, key, key, AdmissionFormLayoutType.SinglePage);
		await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, true);
		var offering = new AdmissionOffering(Guid.NewGuid(), TenantId, cycle.Id, null, board.Id, medium.Id, stage.Id, standard.Id, key, true, null, null);
		await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
		var family = new AdmissionFamily(Guid.NewGuid(), TenantId, key);
		await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family, true);
		var applicant = new AdmissionApplicant(Guid.NewGuid(), TenantId, family.Id, "Credentials", null, "Provider", null, null, null, student.Id);
		await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
		var application = new AdmissionApplication(Guid.NewGuid(), TenantId, key, applicant.Id, null, cycle.Id, offering.Id, workflow.Id, form.Id);
		await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, true);
		var item = new StudentOnboardingCase(Guid.NewGuid(), TenantId, application.Id, applicant.Id, student.Id, offering.Id,
			year.Id, board.Id, medium.Id, stage.Id, standard.Id, key, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
		var cases = services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>(); await cases.InsertAsync(item, true);
		var reservation = new StudentSeatReservation(Guid.NewGuid(), TenantId, item.Id, year.Id, standard.Id, division.Id, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
		await services.GetRequiredService<IRepository<StudentSeatReservation, Guid>>().InsertAsync(reservation, true);
		item.RecordPlacement(division.Id, reservation.Id, DateTime.UtcNow); item.RecordIdentifier(null, 1, DateTime.UtcNow);
		await SeedOnboardingWorklistPostgresRequirementsAsync(services, item, application, student);
		item.MarkReady(DateTime.UtcNow);
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, year.Id, standard.Id, division.Id, 1, item.Id);
		await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
		reservation.Commit(DateTime.UtcNow); await services.GetRequiredService<IRepository<StudentSeatReservation, Guid>>().UpdateAsync(reservation, true);
		item.BeginProvisioning(enrollment.Id); await cases.UpdateAsync(item, true);
		var handoff = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TenantId, item.Id, "guardian-account", true, key + "-guardian");
		await services.GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().InsertAsync(handoff, true);
		var roles = services.GetRequiredService<IdentityRoleManager>();
		if (!await roles.RoleExistsAsync("Parent")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", TenantId))).Succeeded.ShouldBeTrue();
		var model = services.GetRequiredService<ShikshaMitraDbContext>().Model;
		model.FindEntityType(typeof(IdentityUser))!.GetTableName().ShouldBe("AbpUsers");
		model.FindEntityType(typeof(StudentPortalAccountLink))!.GetTableName().ShouldBe("AppStudentPortalAccountLinks");
		model.FindEntityType(typeof(StudentCredentialEmailRequest))!.GetTableName().ShouldBe("AppStudentCredentialEmailRequests");
		return new CredentialsPostgresFixture(item.Id, student.Id, year.Id, enrollment.Id, handoff.Id, email);
	});

	private async Task ConfigureCredentialsOutboxFailureAsync(Guid caseId, string name, bool install)
	{
		// The factory already refuses existing databases. Repeat its exact loopback/name
		// boundary before this gate's test-only DDL, and verify the opened database.
		var builder = new NpgsqlConnectionStringBuilder(factory.ConnectionString);
		if (builder.Host is not ("127.0.0.1" or "localhost" or "::1") || builder.Database == null ||
			!Regex.IsMatch(builder.Database, "^records_tests_[a-z0-9_]{1,48}$", RegexOptions.CultureInvariant) ||
			!Regex.IsMatch(name, "^credentials_gate_[a-f0-9]{32}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Credentials failure injection is restricted to the factory's fresh disposable loopback database.");
		await using var connection = new NpgsqlConnection(builder.ConnectionString); await connection.OpenAsync();
		await using (var check = new NpgsqlCommand("SELECT current_database()", connection))
			((string)(await check.ExecuteScalarAsync())!).ShouldBe(builder.Database);
		var sql = install ? $"""
			CREATE FUNCTION "{name}"() RETURNS trigger LANGUAGE plpgsql AS $gate$
			BEGIN
				IF NEW."CaseId" = '{caseId:D}'::uuid THEN
					IF NOT EXISTS (SELECT 1 FROM "AbpUsers" WHERE "Id" = NEW."UserId")
						OR NOT EXISTS (SELECT 1 FROM "AppStudentPortalAccountLinks" WHERE "Id" = NEW."LinkId" AND "UserId" = NEW."UserId")
						OR NOT EXISTS (SELECT 1 FROM "AppStudentOnboardingServiceHandoffs" WHERE "OnboardingCaseId" = NEW."CaseId" AND "ServiceKey" = 'guardian-account' AND "Status" = {(int)StudentOnboardingServiceHandoffStatus.Acknowledged})
						OR NOT EXISTS (SELECT 1 FROM "AppStudentOnboardingCases" WHERE "Id" = NEW."CaseId" AND "Status" = {(int)StudentOnboardingStatus.Completed}) THEN
						RAISE EXCEPTION 'Credentials gate did not reach flushed access writes' USING ERRCODE = 'P0002';
					END IF;
					RAISE EXCEPTION 'Synthetic outbox rejection after flushed access writes' USING ERRCODE = '23514';
				END IF;
				RETURN NEW;
			END;
			$gate$;
			CREATE TRIGGER "{name}" BEFORE INSERT ON "AppStudentCredentialEmailRequests" FOR EACH ROW EXECUTE FUNCTION "{name}"();
			""" : $"DROP TRIGGER IF EXISTS \"{name}\" ON \"AppStudentCredentialEmailRequests\"; DROP FUNCTION IF EXISTS \"{name}\"();";
		await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
	}

	private sealed record CredentialsPostgresFixture(Guid CaseId, Guid StudentId, Guid YearId, Guid EnrollmentId, Guid HandoffId, string Email);
}
