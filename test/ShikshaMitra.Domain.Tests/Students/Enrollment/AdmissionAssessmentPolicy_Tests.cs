using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentPolicy_Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"formDefinitionId\":\"published-form\"}")]
    [InlineData("{\"assessment\":{\"version\":1,\"enabled\":false}}")]
    public void Missing_And_Disabled_Policies_Preserve_Legacy_Behavior(string? json)
    {
        AdmissionAssessmentPolicy.Read(json).ShouldBeNull();
    }

    [Fact]
    public void Enabled_Policy_Roundtrips_Alongside_Unrelated_Configuration()
    {
        var expected = Policy() with { AttendanceRequired = true, SchedulingEnabled = false };
        var json = JsonSerializer.Serialize(new { formDefinitionId = "published-form", assessment = expected });
        JsonSerializer.Serialize(AdmissionAssessmentPolicy.Read(json)).ShouldBe(JsonSerializer.Serialize(expected));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Unknown_Policy_Versions_Cannot_Enable_Managed_Assessment(int version)
    {
        Should.Throw<ArgumentException>(() => (Policy() with { Version = version }).Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Attempts_Outside_One_To_Ten_Are_Rejected(int attempts)
    {
        Should.Throw<ArgumentException>(() => (Policy() with { MaxAttempts = attempts }).Validate());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(10001, 40)]
    [InlineData(100, -1)]
    [InlineData(100, 101)]
    public void Scored_Policies_Reject_Invalid_Mark_Ranges(int total, int passing)
    {
        Should.Throw<ArgumentException>(() =>
            (Policy() with { TotalMarks = total, PassingMarks = passing }).Validate());
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(10000, 10000, 10)]
    public void Valid_Mark_And_Attempt_Boundaries_Are_Accepted(int total, int passing, int attempts)
    {
        Should.NotThrow(() =>
            (Policy() with { TotalMarks = total, PassingMarks = passing, MaxAttempts = attempts }).Validate());
    }

    [Fact]
    public void Non_Scored_Assessment_Does_Not_Apply_Mark_Limits()
    {
        Should.NotThrow(() =>
            (Policy() with { ScoringEnabled = false, TotalMarks = 0, PassingMarks = -1 }).Validate());
    }

    [Fact]
    public void Malformed_Enabled_Policy_Cannot_Silently_Fall_Back_To_Legacy()
    {
        Should.Throw<Exception>(() => AdmissionAssessmentPolicy.Read("{\"assessment\":true}"));
        Should.Throw<Exception>(() => AdmissionAssessmentPolicy.Read("{\"assessment\":{\"enabled\":true}}"));
    }

    private static AdmissionAssessmentPolicy Policy() => new()
    {
        Version = 1, Enabled = true, ScoringEnabled = true,
        TotalMarks = 100, PassingMarks = 40, MaxAttempts = 1
    };
}
