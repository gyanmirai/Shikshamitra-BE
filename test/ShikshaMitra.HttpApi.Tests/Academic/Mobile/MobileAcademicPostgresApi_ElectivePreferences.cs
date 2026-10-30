using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Portal;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Mobile;

public sealed partial class MobileAcademicPostgresApi_Tests
{
	private const string PreferencesRoute = "/api/mobile/v1/academics/timetable/elective-preferences";
	private const string StaffElectiveRoute = "/api/class-management/student-elective-assignments";

	private async Task CheckElectivePreferencesAsync(HttpClient parent, Fixture f, MobileAcademicPostgresFactory factory, Guid tenantId)
	{
		Guid groupId, firstId, secondId, studentUserId;
		using (var scope = factory.Services.CreateScope())
		{
			Services = scope.ServiceProvider;
			using var unit = Services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			using var tenant = Services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			var first = await Insert(new Subject(Guid.NewGuid(), tenantId, "Hindi", "#008967", f.Student.AcademicMediumId));
			var second = await Insert(new Subject(Guid.NewGuid(), tenantId, "Gujarati", "#008967", f.Student.AcademicMediumId));
			firstId = first.Id; secondId = second.Id;
			var group = new ElectiveGroup(Guid.NewGuid(), tenantId, "Languages", "#008967", f.Student.AcademicMediumId);
			group.AddSubject(first.Id); group.AddSubject(second.Id); await Insert(group); groupId = group.Id;
			foreach (var subject in new[] { first, second })
				await Insert(new AssignedSubject(Guid.NewGuid(), tenantId, f.Enrollment.StandardId!.Value, subject.Id, subject.Name,
					SubjectStatus.Published, SubjectType.Elective, group.Id));
			var timetable = new TimetableEntity(Guid.NewGuid(), tenantId, f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value);
			var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Tuesday, 1, new(9, 30), new(10, 15), SlotType.Period, electiveGroupId: group.Id);
			slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, first.Id));
			slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, second.Id));
			timetable.AddSlot(slot); timetable.Publish(); await Insert(timetable);
			var user = new IdentityUser(Guid.NewGuid(), "elective-student", "elective-student@example.test", tenantId);
			user.SetEmailConfirmed(true); await Insert(user); studentUserId = user.Id;
			await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, f.Student.Id, user.Id, "student", "self", Guid.NewGuid()));
			await unit.CompleteAsync();
		}
		using var student = factory.CreateClient();
		SetElectiveIdentity(student, studentUserId, tenantId, "student");
		var query = $"?studentId={f.Student.Id}&academicYearId={f.Year.Id}";
		var state = await ReadPreferences(parent, query);
		state.StudentId.ShouldBe(f.Student.Id); state.AcademicYearId.ShouldBe(f.Year.Id);
		state.Availability.ShouldBe("electivePreferenceRequired");
		state.Groups.Single().Options.Select(x => x.SubjectId).ShouldBe(new[] { firstId, secondId }, ignoreOrder: true);
		state.Groups.Single().CanSubmit.ShouldBeTrue();
		PortalElectivePreferenceSubmitInput Input(Guid subjectId) => new()
		{
			StudentId = f.Student.Id, AcademicYearId = f.Year.Id, ElectiveGroupId = groupId,
			PreferredSubjectId = subjectId, ClientRequestId = Guid.NewGuid()
		};
		var wrongGroup = Input(firstId); wrongGroup.ElectiveGroupId = Guid.NewGuid();
		foreach (var invalid in new[] { Input(f.SubjectId), wrongGroup })
		{
			using var response = await parent.PostAsJsonAsync(PreferencesRoute, invalid, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		}
		var wrongYear = Input(firstId); wrongYear.AcademicYearId = Guid.NewGuid();
		using (var response = await parent.PostAsJsonAsync(PreferencesRoute, wrongYear, JsonOptions))
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using (var stranger = factory.CreateClient())
		{
			SetElectiveIdentity(stranger, Guid.NewGuid(), tenantId, "parent");
			using var read = await stranger.GetAsync(PreferencesRoute + query);
			read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var write = await stranger.PostAsJsonAsync(PreferencesRoute, Input(firstId), JsonOptions);
			write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		var otherChild = Input(firstId); otherChild.StudentId = Guid.NewGuid();
		using (var response = await parent.PostAsJsonAsync(PreferencesRoute, otherChild, JsonOptions))
			response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using (var otherTenant = factory.CreateClient())
		{
			// A real alternate school avoids testing a tenant-resolver 404 instead of portal isolation.
			Guid otherId;
			using (var scope = factory.Services.CreateScope())
			{
				var services = scope.ServiceProvider;
				using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
				var school = await services.GetRequiredService<Volo.Abp.TenantManagement.TenantManager>().CreateAsync("Other elective school");
				await services.GetRequiredService<Volo.Abp.TenantManagement.ITenantRepository>().InsertAsync(school, true);
				otherId = school.Id; await unit.CompleteAsync();
			}
			SetElectiveIdentity(otherTenant, f.User.Id, otherId, "parent");
			using var response = await otherTenant.PostAsJsonAsync(PreferencesRoute, Input(firstId), JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}

		// Fail the legacy mirror write in PostgreSQL, after the receipt has been tracked, to prove transaction rollback.
		await using (var connection = new NpgsqlConnection(factory.ConnectionString))
		{
			await connection.OpenAsync();
			await using var add = new NpgsqlCommand($"ALTER TABLE \"academic_StudentElectiveSelections\" ADD CONSTRAINT \"TEST_ElectiveAtomicity\" CHECK (\"ElectiveGroupId\" <> '{groupId}')", connection);
			await add.ExecuteNonQueryAsync();
			try
			{
				using var failed = await parent.PostAsJsonAsync(PreferencesRoute, Input(firstId), JsonOptions);
				failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, await failed.Content.ReadAsStringAsync());
				(await ReadElectiveRows(factory, tenantId, f.Student.Id)).ShouldBe((0, 0));
			}
			finally
			{
				await using var drop = new NpgsqlCommand("ALTER TABLE \"academic_StudentElectiveSelections\" DROP CONSTRAINT \"TEST_ElectiveAtomicity\"", connection);
				await drop.ExecuteNonQueryAsync();
			}
		}
		var studentInput = Input(firstId); var parentInput = Input(secondId);
		factory.Barrier.Arm();
		var responses = await Task.WhenAll(student.PostAsJsonAsync(PreferencesRoute, studentInput, JsonOptions),
			parent.PostAsJsonAsync(PreferencesRoute, parentInput, JsonOptions)).WaitAsync(TimeSpan.FromSeconds(60));
		PortalElectivePreferenceSubmitInput winningInput;
		HttpClient winner;
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1, string.Join("\n", await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))));
			responses.Count(x => x.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
			winningInput = responses[0].StatusCode == HttpStatusCode.OK ? studentInput : parentInput;
			winner = responses[0].StatusCode == HttpStatusCode.OK ? student : parent;
		}
		finally { foreach (var response in responses) response.Dispose(); }
		(await ReadElectiveRows(factory, tenantId, f.Student.Id)).ShouldBe((1, 1));
		state = await ReadPreferences(parent, query);
		state.Availability.ShouldBe("electiveApprovalPending"); state.CanViewTimetable.ShouldBeFalse();
		state.Groups.Single().Status.ShouldBe("pendingApproval"); state.Groups.Single().CanSubmit.ShouldBeFalse();
		var submittedAt = state.Groups.Single().SubmittedAt;
		var rowVersions = await ReadElectiveRowVersions(factory, f.Student.Id);
		var originalReceipt = await ReadElectiveReceiptSnapshot(factory, f.Student.Id);
		using (var retry = await winner.PostAsJsonAsync(PreferencesRoute, winningInput, JsonOptions))
			retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
		(await ReadElectiveRows(factory, tenantId, f.Student.Id)).ShouldBe((1, 1));
		(await ReadElectiveRowVersions(factory, f.Student.Id)).ShouldBe(rowVersions, "An idempotent retry must not update either PostgreSQL row.");
		var conflictingRetry = Input(winningInput.PreferredSubjectId == firstId ? secondId : firstId);
		conflictingRetry.ClientRequestId = winningInput.ClientRequestId;
		using (var conflict = await winner.PostAsJsonAsync(PreferencesRoute, conflictingRetry, JsonOptions))
			conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var today = DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday);
		var tuesday = today.AddDays(((int)DayOfWeek.Tuesday - (int)today.DayOfWeek + 7) % 7);
		var scheduleQuery = query + $"&fromDate={tuesday:yyyy-MM-dd}&toDate={tuesday:yyyy-MM-dd}";
		using (var pending = await parent.GetAsync("/api/mobile/v1/academics/timetable/schedule" + scheduleQuery))
		{
			pending.StatusCode.ShouldBe(HttpStatusCode.OK, await pending.Content.ReadAsStringAsync());
			var schedule = (await pending.Content.ReadFromJsonAsync<Result<PortalTimetableScheduleDto>>(JsonOptions))!.Data!;
			schedule.Availability.ShouldBe("electiveApprovalPending"); schedule.Days.SelectMany(x => x.Periods).ShouldBeEmpty();
		}
		using var staff = factory.CreateClient(); SetElectiveIdentity(staff, Guid.Parse(TestAuthHandler.TestUserId), tenantId, "admin");
		var staffQuery = $"?academicMediumId={f.Student.AcademicMediumId}&classId={f.Enrollment.StandardId}&divisionId={f.Enrollment.DivisionId}&electiveGroupId={groupId}";
		using (var visible = await staff.GetAsync(StaffElectiveRoute + staffQuery))
		{
			visible.StatusCode.ShouldBe(HttpStatusCode.OK, await visible.Content.ReadAsStringAsync());
			var records = (await visible.Content.ReadFromJsonAsync<PaginatedStudentElectiveResponseDto>(JsonOptions))!;
			records.Records.Single().SelectedSubjectId.ShouldBe(winningInput.PreferredSubjectId);
			records.Records.Single().StudentPreference.ShouldBe(winningInput.PreferredSubjectId == firstId ? "Hindi" : "Gujarati");
			records.IsLocked.ShouldBeFalse();
		}
		var approvedSubject = winningInput.PreferredSubjectId == firstId ? secondId : firstId;
		using (var save = await staff.PutAsJsonAsync(StaffElectiveRoute, new SaveStudentElectiveAssignmentsDto
		{
			AcademicMediumId = f.Student.AcademicMediumId, ElectiveGroupId = groupId,
			Assignments = [new() { StudentId = f.Student.Id, SelectedSubjectId = approvedSubject }]
		}, JsonOptions)) save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		using (var locked = await staff.PostAsJsonAsync(StaffElectiveRoute + "/lock", new LockStudentElectiveAssignmentsDto
		{
			AcademicMediumId = f.Student.AcademicMediumId, ClassId = f.Enrollment.StandardId!.Value,
			DivisionId = f.Enrollment.DivisionId!.Value, ElectiveGroupId = groupId
		}, JsonOptions)) locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
		state = await ReadPreferences(parent, query);
		state.CanViewTimetable.ShouldBeTrue(); state.Groups.Single().Status.ShouldBe("approved");
		state.Groups.Single().PreferredSubjectId.ShouldBe(winningInput.PreferredSubjectId);
		state.Groups.Single().AssignedSubjectId.ShouldBe(approvedSubject); state.Groups.Single().SubmittedAt.ShouldBe(submittedAt);
		(await ReadElectiveReceiptSnapshot(factory, f.Student.Id)).ShouldBe(originalReceipt, "The school save and lock operations must preserve every original receipt field.");
		using (var approved = await parent.GetAsync("/api/mobile/v1/academics/timetable/schedule" + scheduleQuery))
		{
			approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
			var schedule = (await approved.Content.ReadFromJsonAsync<Result<PortalTimetableScheduleDto>>(JsonOptions))!.Data!;
			schedule.Availability.ShouldBe("available");
			schedule.Days.SelectMany(x => x.Periods).Where(x => x.Subject != null).Select(x => x.Subject!.Id).ShouldBe([approvedSubject]);
		}
		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			var receipt = (await services.GetRequiredService<IRepository<StudentElectivePreferenceSubmission, Guid>>().GetListAsync()).Single();
			receipt.PreferredSubjectId.ShouldBe(winningInput.PreferredSubjectId); receipt.ClientRequestId.ShouldBe(winningInput.ClientRequestId);
			receipt.SubmittingUserId.ShouldBe(winner == student ? studentUserId : f.User.Id);
			await unit.CompleteAsync();
		}
		await VerifyElectiveUniqueConstraints(factory, tenantId, f.Student.Id);
	}

	private static void SetElectiveIdentity(HttpClient client, Guid userId, Guid tenantId, string role)
	{
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, role);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		client.DefaultRequestHeaders.Add("__tenant", tenantId.ToString());
	}
	private static async Task<PortalElectivePreferencesDto> ReadPreferences(HttpClient client, string query)
	{
		using var response = await client.GetAsync(PreferencesRoute + query);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<Result<PortalElectivePreferencesDto>>(JsonOptions))!.Data!;
	}
	private static async Task<(int Receipts, int Selections)> ReadElectiveRows(MobileAcademicPostgresFactory factory, Guid tenantId, Guid studentId)
	{
		await using var connection = new NpgsqlConnection(factory.ConnectionString); await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT (SELECT count(*) FROM \"academic_StudentElectivePreferenceSubmissions\" WHERE \"TenantId\" = @tenant AND \"StudentId\" = @student), (SELECT count(*) FROM \"academic_StudentElectiveSelections\" WHERE \"TenantId\" = @tenant AND \"StudentId\" = @student)", connection);
		command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("student", studentId);
		await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
		return ((int)reader.GetInt64(0), (int)reader.GetInt64(1));
	}
	private static async Task<string> ReadElectiveRowVersions(MobileAcademicPostgresFactory factory, Guid studentId)
	{
		await using var connection = new NpgsqlConnection(factory.ConnectionString); await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT (SELECT xmin::text FROM \"academic_StudentElectivePreferenceSubmissions\" WHERE \"StudentId\" = @student) || '/' || (SELECT xmin::text FROM \"academic_StudentElectiveSelections\" WHERE \"StudentId\" = @student)", connection);
		command.Parameters.AddWithValue("student", studentId);
		return (string)(await command.ExecuteScalarAsync())!;
	}
	private static async Task<string> ReadElectiveReceiptSnapshot(MobileAcademicPostgresFactory factory, Guid studentId)
	{
		await using var connection = new NpgsqlConnection(factory.ConnectionString); await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT row_to_json(receipt)::text FROM \"academic_StudentElectivePreferenceSubmissions\" receipt WHERE \"StudentId\" = @student", connection);
		command.Parameters.AddWithValue("student", studentId);
		return (string)(await command.ExecuteScalarAsync())!;
	}
	private static async Task VerifyElectiveUniqueConstraints(MobileAcademicPostgresFactory factory, Guid tenantId, Guid studentId)
	{
		await using var connection = new NpgsqlConnection(factory.ConnectionString); await connection.OpenAsync();
		foreach (var sameStudent in new[] { true, false })
		{
			await using var command = new NpgsqlCommand("INSERT INTO \"academic_StudentElectivePreferenceSubmissions\" (\"Id\", \"TenantId\", \"StudentId\", \"AcademicYearId\", \"ElectiveGroupId\", \"PreferredSubjectId\", \"PreferredSubjectName\", \"SubmittingUserId\", \"SubmittedAt\", \"ClientRequestId\") SELECT @id, \"TenantId\", " + (sameStudent ? "\"StudentId\"" : "@otherStudent") + ", \"AcademicYearId\", \"ElectiveGroupId\", \"PreferredSubjectId\", \"PreferredSubjectName\", \"SubmittingUserId\", \"SubmittedAt\", " + (sameStudent ? "@request" : "\"ClientRequestId\"") + " FROM \"academic_StudentElectivePreferenceSubmissions\" WHERE \"TenantId\" = @tenant AND \"StudentId\" = @student", connection);
			command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("request", Guid.NewGuid());
			command.Parameters.AddWithValue("otherStudent", Guid.NewGuid()); command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("student", studentId);
			var error = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
			error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
			error.ConstraintName.ShouldBe(sameStudent ? "UX_ElectivePreference_StudentYearGroup" : "UX_ElectivePreference_ClientRequest");
		}
	}
}
