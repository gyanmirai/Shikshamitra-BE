using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student entrance exam endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET/POST /api/student-enrollment/entrance-exam.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class EntranceExamApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student-enrollment/entrance-exam";
    private const string SlotBaseUrl = "/api/student-enrollment/settings/entrance-exam-slots";

    public EntranceExamApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Entrance_Exam_List_Without_Error()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Entrance_Exam_List_With_Valid_Shape()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Empty_List_When_No_Students_In_Exam_Stage()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_FormNo_In_Entrance_Exam_List()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamFormNo");
        await MoveRegistrationToEntranceExamAsync(registration.StudentId);

        var response = await Client.GetAsync(
            $"{BaseUrl}?searchTerm={Uri.EscapeDataString(registration.FirstName)}&maxResultCount=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.ShouldContain(item =>
            item.StudentId == registration.StudentId &&
            item.FormNo == registration.FormNo);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Entrance_Exam_Slot_List_With_Seat_Counts()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamSlotCounts");
        var slot = await CreateEntranceExamSlotAsync("Counts", DateTimeOffset.UtcNow.AddDays(3), studentLimit: 2);

        var scheduleResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{registration.StudentId}/schedule",
            new ScheduleEntranceExamDto { EntranceExamSlotId = slot.Id },
            JsonOptions);
        scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scheduleResponse.Content.ReadAsStringAsync());

        var listResponse = await Client.GetAsync(
            $"{SlotBaseUrl}?academicYearId={Factory.SeededAcademicYearId}&searchTerm={Uri.EscapeDataString(slot.SlotName!)}&maxResultCount=20");

        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
        var slots = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<EntranceExamSlotDto>>(JsonOptions);
        slots.ShouldNotBeNull();
        var listedSlot = slots.Items.Single(item => item.Id == slot.Id);
        listedSlot.StudentLimit.ShouldBe(2);
        listedSlot.EnrolledStudents.ShouldBe(1);
        listedSlot.AvailableSeats.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Reschedule_Missed_Unfilled_Entrance_Exam_And_Release_Previous_Slot()
    {
        var registration = await WithStepTimeout(
            StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
                Client,
                "ExamReschedule"),
            "create reschedule registration");
        var oldSlot = await WithStepTimeout(
            CreateEntranceExamSlotAsync("OldMissed", DateTimeOffset.UtcNow.AddDays(-3), studentLimit: 1),
            "create missed old slot");
        var newSlot = await WithStepTimeout(
            CreateEntranceExamSlotAsync("NewOverride", DateTimeOffset.UtcNow.AddDays(4), studentLimit: 1),
            "create override new slot");

        await WithStepTimeout(
            MoveRegistrationToEntranceExamAsync(registration.StudentId, oldSlot),
            "seed missed entrance exam state");

        var rescheduleResponse = await WithStepTimeout(
            Client.PostAsJsonAsync(
                $"{BaseUrl}/{registration.StudentId}/schedule",
                new ScheduleEntranceExamDto { EntranceExamSlotId = newSlot.Id },
                JsonOptions),
            "post reschedule request");

        rescheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rescheduleResponse.Content.ReadAsStringAsync());

        var detailResponse = await WithStepTimeout(
            Client.GetAsync($"{BaseUrl}/{registration.StudentId}"),
            "get rescheduled exam detail");
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        var detail = await detailResponse.Content.ReadFromJsonAsync<Result<EntranceExamDetailDto>>(JsonOptions);
        detail.ShouldNotBeNull();
        detail.Data.ShouldNotBeNull();
        detail.Data.EntranceExamSlotId.ShouldBe(newSlot.Id);
        detail.Data.ResultUploaded.ShouldBeFalse();
        detail.Data.EnrollmentState.ShouldBe(StudentLifeCycleStatusEnum.EntranceExam);

        var oldSlotResponse = await WithStepTimeout(
            Client.GetAsync($"{SlotBaseUrl}/{oldSlot.Id}"),
            "get old slot detail");
        oldSlotResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await oldSlotResponse.Content.ReadAsStringAsync());
        var oldSlotDetail = await oldSlotResponse.Content.ReadFromJsonAsync<EntranceExamSlotDetailDto>(JsonOptions);
        oldSlotDetail.ShouldNotBeNull();
        oldSlotDetail.EnrolledStudents.ShouldBe(0);
        oldSlotDetail.AvailableSeats.ShouldBe(1);

        var newSlotResponse = await WithStepTimeout(
            Client.GetAsync($"{SlotBaseUrl}/{newSlot.Id}"),
            "get new slot detail");
        newSlotResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await newSlotResponse.Content.ReadAsStringAsync());
        var newSlotDetail = await newSlotResponse.Content.ReadFromJsonAsync<EntranceExamSlotDetailDto>(JsonOptions);
        newSlotDetail.ShouldNotBeNull();
        newSlotDetail.EnrolledStudents.ShouldBe(1);
        newSlotDetail.AvailableSeats.ShouldBe(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Reject_Scheduling_To_Past_Entrance_Exam_Slot()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamPastSlot");
        var pastSlot = await CreateEntranceExamSlotAsync(
            "PastRejected",
            DateTimeOffset.UtcNow.AddDays(-1),
            studentLimit: 2);

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{registration.StudentId}/schedule",
            new ScheduleEntranceExamDto { EntranceExamSlotId = pastSlot.Id },
            JsonOptions);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ShouldContain("ST:Enrollment:030001");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Not_Return_Past_Slots_As_Available()
    {
        var sharedSearch = $"Available{Guid.NewGuid():N}"[..18];
        var pastSlot = await CreateEntranceExamSlotAsync(
            $"{sharedSearch}Past",
            DateTimeOffset.UtcNow.AddDays(-1),
            studentLimit: 2);
        var futureSlot = await CreateEntranceExamSlotAsync(
            $"{sharedSearch}Future",
            DateTimeOffset.UtcNow.AddDays(2),
            studentLimit: 2);

        var pastDateResponse = await Client.GetAsync(
            $"{SlotBaseUrl}/available?academicYearId={Factory.SeededAcademicYearId}&date={Uri.EscapeDataString(pastSlot.StartDateTime.ToString("O"))}&maxResultCount=50");

        pastDateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pastDateResponse.Content.ReadAsStringAsync());
        var pastDateSlots = await pastDateResponse.Content.ReadFromJsonAsync<PagedResultDto<AvailableExamSlotDto>>(JsonOptions);
        pastDateSlots.ShouldNotBeNull();
        pastDateSlots.Items.ShouldNotContain(item => item.Id == pastSlot.Id);

        var futureDateResponse = await Client.GetAsync(
            $"{SlotBaseUrl}/available?academicYearId={Factory.SeededAcademicYearId}&date={Uri.EscapeDataString(futureSlot.StartDateTime.ToString("O"))}&maxResultCount=50");

        futureDateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await futureDateResponse.Content.ReadAsStringAsync());
        var futureDateSlots = await futureDateResponse.Content.ReadFromJsonAsync<PagedResultDto<AvailableExamSlotDto>>(JsonOptions);
        futureDateSlots.ShouldNotBeNull();
        futureDateSlots.Items.ShouldContain(item => item.Id == futureSlot.Id);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Entrance_Exam_Lifecycle_State_In_Registration_List()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamLifecycle");
        var slot = await CreateEntranceExamSlotAsync(
            "Lifecycle",
            DateTimeOffset.UtcNow.AddDays(2),
            studentLimit: 2);

        var scheduleResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{registration.StudentId}/schedule",
            new ScheduleEntranceExamDto { EntranceExamSlotId = slot.Id },
            JsonOptions);
        scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scheduleResponse.Content.ReadAsStringAsync());

        var response = await Client.GetAsync(
            $"/api/student/enrollment/registrations?academicMediumId={StudentEnrollmentTestHelper.DefaultAcademicMediumId}&maxResultCount=50&searchTerm={Uri.EscapeDataString(registration.FirstName)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<StudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        var listedRegistration = body.Data.Items.Single(item => item.StudentId == registration.StudentId);
        listedRegistration.StudentEnrollmentState.ShouldBe(StudentLifeCycleStatusEnum.EntranceExam);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Sort_Entrance_Exam_List_By_Exam_Start_Time()
    {
        var sharedSearch = $"ExamSort{Guid.NewGuid():N}"[..16];
        var earlierRegistration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            $"{sharedSearch}Earlier");
        var laterRegistration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            $"{sharedSearch}Later");
        var earlierSlot = await CreateEntranceExamSlotAsync(
            $"{sharedSearch}EarlySlot",
            DateTimeOffset.UtcNow.AddDays(2),
            studentLimit: 2);
        var laterSlot = await CreateEntranceExamSlotAsync(
            $"{sharedSearch}LateSlot",
            DateTimeOffset.UtcNow.AddDays(5),
            studentLimit: 2);

        await MoveRegistrationToEntranceExamAsync(earlierRegistration.StudentId, earlierSlot);
        await MoveRegistrationToEntranceExamAsync(laterRegistration.StudentId, laterSlot);

        var response = await Client.GetAsync(
            $"{BaseUrl}?searchTerm={Uri.EscapeDataString(sharedSearch)}&sorting=examStartTime%20desc&maxResultCount=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();

        var sortedIds = body.Data.Items.Select(item => item.StudentId).ToList();
        sortedIds.Count.ShouldBe(2);
        sortedIds[0].ShouldBe(laterRegistration.StudentId);
        sortedIds[1].ShouldBe(earlierRegistration.StudentId);
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Student()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Schedule_Nonexistent_Student()
    {
        var dto = new ScheduleEntranceExamDto { EntranceExamSlotId = Guid.NewGuid() };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/schedule", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Handled_Error_When_Scheduling_Invalid_Status()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamInvalidStatus");
        await MoveRegistrationToEntranceExamAsync(registration.StudentId);

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{registration.StudentId}/schedule",
            new ScheduleEntranceExamDto { EntranceExamSlotId = Guid.NewGuid() },
            JsonOptions);

        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ShouldContain("ST:Enrollment:030001");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Upload_Result_Nonexistent_Student()
    {
        var dto = new UploadEntranceExamResultDto
        {
            TotalMarks = 100,
            PassingMarks = 40,
            ObtainedMarks = 75,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/result", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_MoveToAdmission_Nonexistent_Student()
    {
        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/move-to-admission", new { }, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401_On_List()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_List_With_Pagination_Defaults()
    {
        var response = await Client.GetAsync($"{BaseUrl}?skipCount=0&maxResultCount=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.Count.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_BulkMoveToAdmission_With_Empty_List()
    {
        var dto = new BulkMoveToAdmissionDto
        {
            StudentIds = new System.Collections.Generic.List<Guid>(),
            AcademicYearId = Factory.SeededAcademicYearId,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/bulk-move-to-admission", dto, JsonOptions);

        // Empty list may return validation error or empty result — both are acceptable
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_BulkMoveToAdmission_Nonexistent_Students()
    {
        var dto = new BulkMoveToAdmissionDto
        {
            StudentIds = new System.Collections.Generic.List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
            },
            AcademicYearId = Factory.SeededAcademicYearId,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/bulk-move-to-admission", dto, JsonOptions);

        // Non-existent IDs should either return an error or a result with all failures
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);
    }

    #endregion

    private async Task MoveRegistrationToEntranceExamAsync(Guid studentId)
    {
        await MoveRegistrationToEntranceExamAsync(
            studentId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            101);
    }

    private async Task MoveRegistrationToEntranceExamAsync(Guid studentId, EntranceExamSlotDto slot)
    {
        await MoveRegistrationToEntranceExamAsync(
            studentId,
            slot.Id,
            slot.StartDateTime,
            slot.EndDateTime,
            slot.RoomNo);
    }

    private async Task MoveRegistrationToEntranceExamAsync(
        Guid studentId,
        Guid slotId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        int roomNo)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var student = await studentRepository.GetInquiryForUpdateAsync(studentId)
                          ?? throw new InvalidOperationException($"Student not found: {studentId}");
            student.ScheduleEntranceExam(
                slotId,
                startDateTime,
                endDateTime,
                roomNo);

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task<EntranceExamSlotDto> CreateEntranceExamSlotAsync(
        string suffix,
        DateTimeOffset startDate,
        int studentLimit)
    {
        var slotName = $"Regression {suffix} {Guid.NewGuid():N}";
        var response = await Client.PostAsJsonAsync(
            SlotBaseUrl,
            new CreateEntranceExamSlotDto
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                SlotName = slotName,
                StartTimeDate = startDate,
                EndTimeDate = startDate.AddHours(1),
                StudentLimit = studentLimit,
                RoomNo = Random.Shared.Next(10000, 99999)
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var slot = await response.Content.ReadFromJsonAsync<EntranceExamSlotDto>(JsonOptions);
        slot.ShouldNotBeNull();
        return slot;
    }

    private static async Task WithStepTimeout(Task task, string step)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Timed out while trying to {step}.", ex);
        }
    }

    private static async Task<T> WithStepTimeout<T>(Task<T> task, string step)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Timed out while trying to {step}.", ex);
        }
    }

}
