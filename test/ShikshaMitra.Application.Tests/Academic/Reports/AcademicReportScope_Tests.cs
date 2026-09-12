using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
    [InlineData("Create a homework completion report", AcademicReportModules.Homework)]
    [InlineData("Show the examination marks", AcademicReportModules.Examination)]
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
            ["subject", "teacherName", "teacherType"])
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
        EducationMediumId = Guid.NewGuid()
    };
}
