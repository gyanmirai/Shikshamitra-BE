using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportScope_Tests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("short", false)]
    [InlineData("Create PDF", true)]
    public void Ai_Report_Prompt_Should_Enforce_Contract_Limits(string prompt, bool shouldBeValid)
    {
        var input = ValidAiExportRequest(prompt);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, true)
            .ShouldBe(shouldBeValid);
    }

    [Fact]
    public void Ai_Report_Prompt_Should_Reject_More_Than_2000_Characters()
    {
        var input = ValidAiExportRequest(new string('a', 2001));
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, true)
            .ShouldBeFalse();
    }

    [Fact]
    public void Ai_Report_Should_Require_Selected_Academic_Context()
    {
        var input = new AcademicAiReportPlanRequestDto
        {
            Prompt = "Create a curriculum subjects report"
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, true)
            .ShouldBeFalse();
        results.Count.ShouldBe(3);
    }

    [Fact]
    public void Multi_Agent_Response_Should_Use_Only_Root_Final_Answer()
    {
        const string response = """
            {
              "output": [
                {
                  "agent": { "agent_name": "/root/classifier" },
                  "phase": "final_answer",
                  "content": [{ "type": "output_text", "text": "subagent result" }]
                },
                {
                  "agent": { "agent_name": "/root" },
                  "phase": "final_answer",
                  "content": [{ "type": "output_text", "text": "root result" }]
                }
              ]
            }
            """;

        AcademicAiReportPlanner.ReadOutputText(response, useMultiAgent: true)
            .ShouldBe("root result");
    }

    [Fact]
    public void Zai_Chat_Completion_Should_Read_Json_Content()
    {
        const string response = """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "{\"reportType\":\"Curriculum Completion Report\"}"
                  },
                  "finish_reason": "stop"
                }
              ]
            }
            """;

        AcademicAiReportPlanner.ReadChatCompletionText(response)
            .ShouldBe("{\"reportType\":\"Curriculum Completion Report\"}");
    }

    [Fact]
    public void Zai_Chat_Completion_Should_Remove_Markdown_Fence()
    {
        const string response = """
            {
              "choices": [
                {
                  "message": {
                    "content": "```json\n{\"status\":\"ok\"}\n```"
                  }
                }
              ]
            }
            """;

        AcademicAiReportPlanner.ReadChatCompletionText(response)
            .ShouldBe("{\"status\":\"ok\"}");
    }

    [Theory]
    [InlineData("Create a homework completion report", AcademicReportModules.HomeworkAssignment)]
    [InlineData("Create an assignment submission report", AcademicReportModules.HomeworkAssignment)]
    [InlineData("Show the examination marks", AcademicReportModules.Examination)]
    [InlineData("Show student attendance below 75%", AcademicReportModules.StudentAttendance)]
    [InlineData("Create an employee payroll report", AcademicReportModules.HumanResources)]
    [InlineData("Show outstanding fees by class", AcademicReportModules.Finance)]
    [InlineData("List overdue books", AcademicReportModules.Library)]
    [InlineData("Create a bus route report", AcademicReportModules.Transport)]
    [InlineData("Show student enrollment by class", AcademicReportModules.StudentRecords)]
    [InlineData("Show LMS learning analytics", AcademicReportModules.LearningManagement)]
    [InlineData("Create the Grade 8 curriculum report", null)]
    [InlineData("Create a subject assignment report", null)]
    [InlineData("Show chapter marks weightage", null)]
    [InlineData("Show subject and chapter coverage", null)]
    public void Ai_Report_Guard_Should_Detect_Only_Other_Module_Requests(
        string prompt,
        string? requestedModule)
    {
        AcademicDynamicReportGuard.DetectRequestedModule(prompt).ShouldBe(requestedModule);
    }

    [Fact]
    public void Ai_Report_Plan_Should_Repair_Missing_Dataset_For_Subject_Primary_Teacher_Request()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Curriculum,
            Dataset = string.Empty,
            Columns = ["subjectName", "primary_teacher"]
        };

        AcademicAiReportPlanNormalizer.Normalize(
            "I want to generate the report of the subject with its primary teacher for 2026-2027 CBSE English. I want only two columns: subject name and primary teacher",
            AcademicReportModules.Curriculum,
            plan,
            DynamicDatasets())
            .ShouldSatisfyAllConditions(
                normalized => normalized.Dataset.ShouldBe(AcademicDynamicDatasets.CurriculumSubjects),
                normalized => normalized.Columns.ShouldBe(["subject", "primaryTeacher"]));
    }

    [Fact]
    public void Ai_Report_Plan_Should_Correct_Cross_Dataset_Subject_Columns()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Curriculum,
            Dataset = AcademicDynamicDatasets.CurriculumTeachers,
            Columns = ["subject", "primaryTeacher"]
        };

        AcademicAiReportPlanNormalizer.Normalize(
            "Show every subject with its primary teacher",
            AcademicReportModules.Curriculum,
            plan,
            DynamicDatasets())
            .Dataset.ShouldBe(AcademicDynamicDatasets.CurriculumSubjects);
    }

    [Fact]
    public void Ai_Report_Plan_Should_Enforce_Translated_Exact_Subject_Columns()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Curriculum,
            Dataset = AcademicDynamicDatasets.CurriculumSubjects,
            Columns = ["class", "subject", "primaryTeacher"]
        };

        AcademicAiReportPlanNormalizer.Normalize(
            "Create a report for Nursery class showing each subject name and its primary teacher only.",
            AcademicReportModules.Curriculum,
            plan,
            DynamicDatasets())
            .Columns.ShouldBe(["subject", "primaryTeacher"]);
    }

    [Fact]
    public void Ai_Report_Plan_Should_Not_Guess_An_Unsupported_Dataset()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Curriculum,
            Dataset = string.Empty,
            Columns = ["attendancePercentage"]
        };

        AcademicAiReportPlanNormalizer.Normalize(
            "Show student attendance percentages",
            AcademicReportModules.Curriculum,
            plan,
            DynamicDatasets())
            .Dataset.ShouldBeEmpty();
    }

    [Fact]
    public void Ai_Report_Filter_Resolver_Should_Apply_Explicit_Class_From_Prompt()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Filters = new AcademicDynamicReportFilterDto()
        };

        var resolved = AcademicDynamicReportFilterResolver.Resolve(
            "Give me only the Nursery class subjects",
            plan,
            FilterSnapshot());

        resolved.Filters.ClassTokens.ShouldBe(["class_1"]);
        resolved.RequestedFilterTypes.ShouldContain(AcademicDynamicFilterTypes.Class);
        resolved.AppliedFilters.Single().Values.ShouldBe(["Nursery"]);
    }

    [Fact]
    public void Ai_Report_Filter_Resolver_Should_Use_Translated_Interpretation()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            InterpretedRequest = "Show only subjects for the Nursery class",
            RequestedFilterTypes = [AcademicDynamicFilterTypes.Class],
            Filters = new AcademicDynamicReportFilterDto()
        };

        var resolved = AcademicDynamicReportFilterResolver.Resolve(
            "મને ફક્ત નર્સરી વર્ગના વિષયો બતાવો",
            plan,
            FilterSnapshot());

        resolved.Filters.ClassTokens.ShouldBe(["class_1"]);
        resolved.AppliedFilters.Single().Values.ShouldBe(["Nursery"]);
    }

    [Fact]
    public void Ai_Report_Filter_Resolver_Should_Override_Wrong_Model_Token_With_Explicit_Class()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            RequestedFilterTypes = [AcademicDynamicFilterTypes.Class],
            Filters = new AcademicDynamicReportFilterDto { ClassTokens = ["class_2"] }
        };

        AcademicDynamicReportFilterResolver.Resolve(
                "Show subjects for Nursery class only",
                plan,
                FilterSnapshot())
            .Filters.ClassTokens.ShouldBe(["class_1"]);
    }

    [Fact]
    public void Ai_Report_Filter_Resolver_Should_Fail_Closed_When_Declared_Filter_Has_No_Value()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            InterpretedRequest = "Show subjects for the requested class",
            RequestedFilterTypes = [AcademicDynamicFilterTypes.Class],
            Filters = new AcademicDynamicReportFilterDto()
        };

        var exception = Should.Throw<BusinessException>(() =>
            AcademicDynamicReportFilterResolver.Resolve("Show my requested class", plan, FilterSnapshot()));

        exception.Code.ShouldBe("Academic:Reports:AiFilterCouldNotBeResolved");
    }

    [Theory]
    [InlineData("{\"Kind\":\"dynamic\",\"Module\":\"Curriculum\",\"Plan\":{}}")]
    [InlineData("{ \"kind\" : \"dynamic\", \"module\" : \"Curriculum\", \"plan\" : {} }")]
    public void Dynamic_Export_Metadata_Should_Be_Recognized_Regardless_Of_Json_Casing_Or_Whitespace(
        string requestJson)
    {
        var request = AcademicReportAppService.TryReadDynamicRequest(requestJson);

        request.ShouldNotBeNull();
        request.Module.ShouldBe(AcademicReportModules.Curriculum);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reportType\":\"Curriculum Completion Report\"}")]
    [InlineData("not-json")]
    public void Static_Or_Invalid_Export_Metadata_Should_Not_Be_Treated_As_Dynamic(string requestJson)
    {
        AcademicReportAppService.TryReadDynamicRequest(requestJson).ShouldBeNull();
    }

    [Fact]
    public void Admin_With_ClassTeacher_Assignment_Should_Keep_Tenant_Wide_Access()
    {
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            IsAdmin = true
        };

        AcademicReportAppService.ShouldApplyClassTeacherRestriction(true, scope).ShouldBeFalse();
    }

    [Fact]
    public void Scoped_ClassTeacher_Should_Keep_Division_Restriction()
    {
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            DivisionIds = [Guid.NewGuid()]
        };

        AcademicReportAppService.ShouldApplyClassTeacherRestriction(true, scope).ShouldBeTrue();
    }

    [Fact]
    public void Ai_Report_Approval_Should_Bind_User_Context_And_Exact_Plan()
    {
        var service = new AcademicAiReportApprovalService(new EphemeralDataProtectionProvider());
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var scope = new AcademicAiReportCatalogScope(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], [Guid.NewGuid()]);
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Curriculum,
            Dataset = AcademicDynamicDatasets.CurriculumSubjects,
            Title = "Approved curriculum report",
            Columns = ["subject", "primaryTeacher"],
            Format = AcademicReportFormat.Excel
        };

        var token = service.CreateToken(userId, tenantId, scope, plan);

        Should.NotThrow(() => service.ValidateToken(token, userId, tenantId, scope, plan));
        plan.Columns = ["subject"];
        var exception = Should.Throw<BusinessException>(() =>
            service.ValidateToken(token, userId, tenantId, scope, plan));
        exception.Code.ShouldBe("Academic:Reports:AiApprovalInvalid");
    }

    [Fact]
    public void Ai_Report_Csv_Should_Escape_Commas_Quotes_And_New_Lines()
    {
        var report = new AcademicReportPreviewDto
        {
            Columns = [new AcademicReportColumnDto { Id = "subject", Label = "Subject, name" }],
            Rows = [new Dictionary<string, string?> { ["subject"] = "English \"A\"\nAdvanced" }]
        };

        var csv = System.Text.Encoding.UTF8.GetString(new AcademicCsvService().GenerateReportCsv(report));

        csv.ShouldContain("\"Subject, name\"");
        csv.ShouldContain("\"English \"\"A\"\"\nAdvanced\"");
    }

    [Theory]
    [InlineData("68%", "lt", "70", "number", true)]
    [InlineData("72", "gte", "75", "number", false)]
    [InlineData("Advanced Mathematics", "contains", "math", "text", true)]
    [InlineData("15 Sep 2026", "between", "2026-09-01|2026-09-30", "date", true)]
    public void Ai_Report_Dynamic_Conditions_Should_Use_Typed_Comparisons(
        string actual,
        string filterOperator,
        string expected,
        string dataType,
        bool shouldMatch)
    {
        var condition = new AcademicDynamicReportConditionDto
        {
            Field = "value",
            Operator = filterOperator,
            Values = expected.Split('|').ToList()
        };
        var row = new Dictionary<string, string?> { ["value"] = actual };
        var field = new AcademicDynamicColumnDefinition("value", "Value", dataType);

        AcademicDynamicReportQueryService.MatchesCondition(row, condition, field)
            .ShouldBe(shouldMatch);
    }

    [Theory]
    [InlineData("Daily attendance percentage summary", AcademicDynamicDatasets.StudentAttendanceDailySummary)]
    [InlineData("List absent students with remarks", AcademicDynamicDatasets.StudentAttendanceEntries)]
    public void Attendance_Request_Should_Infer_The_Correct_Row_Grain(string prompt, string expectedDataset)
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.StudentAttendance,
            Dataset = "unknown",
            Columns = []
        };

        AcademicAiReportPlanNormalizer.Normalize(
                prompt, AcademicReportModules.StudentAttendance, plan, DynamicDatasets())
            .Dataset.ShouldBe(expectedDataset);
    }

    [Fact]
    public void Student_Records_Request_Should_Use_The_Privacy_Minimized_Student_Dataset()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.StudentRecords,
            Dataset = "unknown",
            Columns = []
        };

        var normalized = AcademicAiReportPlanNormalizer.Normalize(
            "List enrolled students by class and division",
            AcademicReportModules.StudentRecords,
            plan,
            DynamicDatasets());

        normalized.Dataset.ShouldBe(AcademicDynamicDatasets.StudentRecordsStudents);
    }

    [Fact]
    public void Timetable_Request_Should_Use_The_Scheduled_Slot_Dataset()
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Timetable,
            Dataset = "unknown",
            Columns = []
        };

        AcademicAiReportPlanNormalizer.Normalize(
                "Show the weekly timetable with teachers and rooms",
                AcademicReportModules.Timetable,
                plan,
                DynamicDatasets())
            .Dataset.ShouldBe(AcademicDynamicDatasets.TimetableSlots);
    }

    [Theory]
    [InlineData("Show homework due this week", AcademicDynamicDatasets.HomeworkTasks)]
    [InlineData("Show assignments with marks and rubric", AcademicDynamicDatasets.AssignmentTasks)]
    public void Homework_Assignment_Request_Should_Infer_The_Correct_Task_Dataset(
        string prompt,
        string expectedDataset)
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.HomeworkAssignment,
            Dataset = "unknown",
            Columns = []
        };

        AcademicAiReportPlanNormalizer.Normalize(
                prompt, AcademicReportModules.HomeworkAssignment, plan, DynamicDatasets())
            .Dataset.ShouldBe(expectedDataset);
    }

    [Theory]
    [InlineData("Show the exam schedule by subject", AcademicDynamicDatasets.ExaminationSchedule)]
    [InlineData("List subject marks and absent students", AcademicDynamicDatasets.ExaminationMarks)]
    [InlineData("Show overall results with grades and ranks", AcademicDynamicDatasets.ExaminationResults)]
    public void Examination_Request_Should_Infer_The_Correct_Row_Grain(
        string prompt,
        string expectedDataset)
    {
        var plan = new AcademicDynamicReportSpecDto
        {
            Module = AcademicReportModules.Examination,
            Dataset = "unknown",
            Columns = []
        };

        AcademicAiReportPlanNormalizer.Normalize(
                prompt, AcademicReportModules.Examination, plan, DynamicDatasets())
            .Dataset.ShouldBe(expectedDataset);
    }

    [Theory]
    [InlineData(" student records ", AcademicReportModules.StudentRecords)]
    [InlineData("TIMETABLE", AcademicReportModules.Timetable)]
    [InlineData("student attendance", AcademicReportModules.StudentAttendance)]
    [InlineData("homework", AcademicReportModules.HomeworkAssignment)]
    [InlineData("Assignment", AcademicReportModules.HomeworkAssignment)]
    [InlineData("homework & assignment", AcademicReportModules.HomeworkAssignment)]
    public void Module_Normalization_Should_Return_The_Canonical_Value(string input, string expected)
    {
        AcademicDynamicReportGuard.NormalizeModule(input).ShouldBe(expected);
    }

    private static IReadOnlyList<AcademicDynamicDatasetDefinition> DynamicDatasets() =>
    [
        new(
            AcademicDynamicDatasets.CurriculumSubjects,
            AcademicReportModules.Curriculum,
            "Curriculum subjects",
            "Subject rows",
            [new("class", "Class"), new("subject", "Subject"), new("primaryTeacher", "Primary teacher")],
            ["subject", "primaryTeacher"]),
        new(
            AcademicDynamicDatasets.CurriculumChapters,
            AcademicReportModules.Curriculum,
            "Curriculum chapters",
            "Chapter rows",
            [new("subject", "Subject"), new("chapterName", "Chapter")],
            ["subject", "chapterName"]),
        new(
            AcademicDynamicDatasets.CurriculumTeachers,
            AcademicReportModules.Curriculum,
            "Curriculum subject teachers",
            "Teacher rows",
            [new("subject", "Subject"), new("teacherName", "Teacher"), new("teacherType", "Teacher type")],
            ["subject", "teacherName", "teacherType"]),
        new(
            AcademicDynamicDatasets.TimetableSlots,
            AcademicReportModules.Timetable,
            "Timetable slots",
            "Timetable rows",
            [new("class", "Class"), new("division", "Division"), new("day", "Day"), new("subject", "Subject")],
            ["class", "division", "day", "subject"]),
        new(
            AcademicDynamicDatasets.StudentRecordsStudents,
            AcademicReportModules.StudentRecords,
            "Student records",
            "Student rows",
            [new("class", "Class"), new("division", "Division"), new("studentName", "Student"), new("lifecycleStatus", "Lifecycle status")],
            ["class", "division", "studentName", "lifecycleStatus"]),
        new(
            AcademicDynamicDatasets.StudentAttendanceEntries,
            AcademicReportModules.StudentAttendance,
            "Student attendance entries",
            "Attendance entry rows",
            [new("date", "Date", AcademicDynamicFieldTypes.Date), new("studentName", "Student"), new("status", "Status")],
            ["date", "studentName", "status"]),
        new(
            AcademicDynamicDatasets.StudentAttendanceDailySummary,
            AcademicReportModules.StudentAttendance,
            "Student attendance daily summary",
            "Attendance summary rows",
            [new("date", "Date", AcademicDynamicFieldTypes.Date), new("attendancePercentage", "Attendance percentage", AcademicDynamicFieldTypes.Number)],
            ["date", "attendancePercentage"]),
        new(
            AcademicDynamicDatasets.HomeworkTasks,
            AcademicReportModules.HomeworkAssignment,
            "Homework tasks",
            "Homework rows",
            [new("class", "Class"), new("title", "Homework title"), new("dueDate", "Due date", AcademicDynamicFieldTypes.Date)],
            ["class", "title", "dueDate"]),
        new(
            AcademicDynamicDatasets.AssignmentTasks,
            AcademicReportModules.HomeworkAssignment,
            "Assignments",
            "Assignment rows",
            [new("class", "Class"), new("title", "Assignment title"), new("totalMarks", "Total marks", AcademicDynamicFieldTypes.Number)],
            ["class", "title", "totalMarks"]),
        new(
            AcademicDynamicDatasets.ExaminationSchedule,
            AcademicReportModules.Examination,
            "Examination schedule",
            "Schedule rows",
            [new("examName", "Exam"), new("subject", "Subject"), new("examDate", "Exam date", AcademicDynamicFieldTypes.Date)],
            ["examName", "subject", "examDate"]),
        new(
            AcademicDynamicDatasets.ExaminationMarks,
            AcademicReportModules.Examination,
            "Student examination marks",
            "Mark rows",
            [new("studentName", "Student"), new("marksObtained", "Marks obtained", AcademicDynamicFieldTypes.Number)],
            ["studentName", "marksObtained"]),
        new(
            AcademicDynamicDatasets.ExaminationResults,
            AcademicReportModules.Examination,
            "Student examination results",
            "Result rows",
            [new("studentName", "Student"), new("grade", "Grade"), new("sectionRank", "Section rank", AcademicDynamicFieldTypes.Number)],
            ["studentName", "grade", "sectionRank"])
    ];

    private static AcademicDynamicReportCatalogSnapshot FilterSnapshot() => new(
        [
            new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Nursery", "class_1"),
            new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Class 7", "class_2")
        ],
        [],
        [],
        []);

    private static AcademicAiReportExportRequestDto ValidAiExportRequest(string prompt) => new()
    {
        Prompt = prompt,
        AcademicYearId = Guid.NewGuid(),
        EducationBoardId = Guid.NewGuid(),
        EducationMediumId = Guid.NewGuid(),
        ApprovalToken = "test-approval-token"
    };
}
