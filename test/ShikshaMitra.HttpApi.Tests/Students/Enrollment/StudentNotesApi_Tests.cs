using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Notes;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student enrollment notes endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET/POST/PUT/DELETE /api/student/enrollment/{studentId}/notes.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentNotesApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student/enrollment";

    public StudentNotesApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Note_And_Return_NoContent()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteCreate");

        var dto = new CreateUpdateStudentNoteDto
        {
            Note = "First counselling done. Student is interested.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(7),
            Status = NoteStatusEnum.Interested,
        };

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Notes_After_Create()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteGetAfterCreate");

        var dto = new CreateUpdateStudentNoteDto
        {
            Note = "Followup done. Parents want more info.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(3),
            Status = NoteStatusEnum.Pending,
        };
        await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", dto, JsonOptions);

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/notes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notes = await response.Content
            .ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBeGreaterThanOrEqualTo(1);
        notes.ShouldContain(n => n.Note == "Followup done. Parents want more info.");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_Note()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteUpd");

        var createDto = new CreateUpdateStudentNoteDto
        {
            Note = "Original note text.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(5),
            Status = NoteStatusEnum.Pending,
        };
        await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", createDto, JsonOptions);

        var notes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBeGreaterThan(0);
        var noteId = notes[0].Id;

        var updateDto = new CreateUpdateStudentNoteDto
        {
            Note = "Updated note text.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(10),
            Status = NoteStatusEnum.Interested,
        };
        var updateResponse = await Client.PutAsJsonAsync(
            $"{BaseUrl}/notes/{noteId}", updateDto, JsonOptions);

        updateResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Delete_Note()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteDel");

        var dto = new CreateUpdateStudentNoteDto
        {
            Note = "Note to be deleted.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(1),
            Status = NoteStatusEnum.Pending,
        };
        await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", dto, JsonOptions);

        var notes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBeGreaterThan(0);
        var noteId = notes[0].Id;

        var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/notes/{noteId}");

        deleteResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        var afterDelete = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        afterDelete.ShouldNotBeNull();
        afterDelete.ShouldNotContain(n => n.Id == noteId);
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Empty_List_For_Student_With_No_Notes()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteEmpty");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/notes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notes = await response.Content
            .ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBe(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Persist_Note_Status()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteStatus");

        var dto = new CreateUpdateStudentNoteDto
        {
            Note = "Checking status persistence.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(2),
            Status = NoteStatusEnum.NotInterested,
        };
        await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", dto, JsonOptions);

        var notes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBeGreaterThan(0);
        notes.ShouldContain(n => n.Status == NoteStatusEnum.NotInterested);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteAuth");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/notes");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Create_Multiple_Notes_For_Same_Student()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteMulti");

        for (var i = 1; i <= 3; i++)
        {
            var dto = new CreateUpdateStudentNoteDto
            {
                Note = $"Counselling session {i} completed.",
                NextReminderDate = DateTimeOffset.UtcNow.AddDays(i * 3),
                Status = NoteStatusEnum.Pending,
            };
            await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", dto, JsonOptions);
        }

        var notes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        notes.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Isolate_Notes_Between_Students()
    {
        var id1 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteIso1");
        var id2 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteIso2");

        var note1Dto = new CreateUpdateStudentNoteDto
        {
            Note = "Note for student one only.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(1),
            Status = NoteStatusEnum.Interested,
        };
        var note2Dto = new CreateUpdateStudentNoteDto
        {
            Note = "Note for student two only.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(2),
            Status = NoteStatusEnum.NotInterested,
        };

        await Client.PostAsJsonAsync($"{BaseUrl}/{id1}/notes", note1Dto, JsonOptions);
        await Client.PostAsJsonAsync($"{BaseUrl}/{id2}/notes", note2Dto, JsonOptions);

        var notes1 = await (await Client.GetAsync($"{BaseUrl}/{id1}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        var notes2 = await (await Client.GetAsync($"{BaseUrl}/{id2}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);

        notes1.ShouldNotBeNull();
        notes1.ShouldContain(n => n.Note == "Note for student one only.");
        notes1.ShouldNotContain(n => n.Note == "Note for student two only.");

        notes2.ShouldNotBeNull();
        notes2.ShouldContain(n => n.Note == "Note for student two only.");
        notes2.ShouldNotContain(n => n.Note == "Note for student one only.");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Reflect_Updated_Note_Text_After_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "NoteUpdText");

        var createDto = new CreateUpdateStudentNoteDto
        {
            Note = "Before update.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(5),
            Status = NoteStatusEnum.Pending,
        };
        await Client.PostAsJsonAsync($"{BaseUrl}/{studentId}/notes", createDto, JsonOptions);

        var notes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        notes.ShouldNotBeNull();
        var noteId = notes[0].Id;

        var updateDto = new CreateUpdateStudentNoteDto
        {
            Note = "After update.",
            NextReminderDate = DateTimeOffset.UtcNow.AddDays(10),
            Status = NoteStatusEnum.Interested,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/notes/{noteId}", updateDto, JsonOptions);

        var updatedNotes = await (await Client.GetAsync($"{BaseUrl}/{studentId}/notes"))
            .Content.ReadFromJsonAsync<List<StudentNoteDto>>(JsonOptions);
        updatedNotes.ShouldNotBeNull();
        updatedNotes.ShouldContain(n => n.Note == "After update.");
        updatedNotes.ShouldContain(n => n.Status == NoteStatusEnum.Interested);
    }

    #endregion
}
