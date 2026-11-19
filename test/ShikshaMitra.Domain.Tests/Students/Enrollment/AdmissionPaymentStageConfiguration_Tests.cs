using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentStageConfiguration_Tests
{
    [Theory]
    [InlineData("{\"version\":1,\"paymentRule\":\"full\",\"allowCollectionLater\":false}")]
    [InlineData("{\"version\":99,\"paymentRule\":\"unsupported\"}")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Retired_Payment_settings_are_removed_without_discarding_extensions(string legacy)
    {
        var json = "{\"payment\":{\"amount\":500},\"paymentCompletion\":" + legacy + ",\"extension\":\"keep\"}";
        var normalized = AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment, json);
        using var parsed = JsonDocument.Parse(normalized!);
        parsed.RootElement.TryGetProperty("payment", out _).ShouldBeFalse();
        parsed.RootElement.TryGetProperty("paymentCompletion", out _).ShouldBeFalse();
        parsed.RootElement.GetProperty("extension").GetString().ShouldBe("keep");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"paymentCompletion\":{\"paymentRule\":\"full\"}}")]
    public void Payment_without_active_settings_has_no_configuration(string? json) =>
        AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment, json).ShouldBeNull();

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    public void Malformed_stage_root_is_still_rejected(string json) =>
        Should.Throw<Exception>(() => AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment, json));
}
