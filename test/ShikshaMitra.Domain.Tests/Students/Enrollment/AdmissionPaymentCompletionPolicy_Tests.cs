using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentCompletionPolicy_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{}")]
	[InlineData("{\"payment\":{\"amount\":500}}")]
	public void Existing_workflows_default_to_partial_and_allow_deferral(string? json)
	{
		var policy = AdmissionPaymentCompletionPolicy.Read(json);
		policy.PaymentRule.ShouldBe("partial"); policy.AllowCollectionLater.ShouldBeTrue();
	}
	[Theory]
	[InlineData("partial", true)]
	[InlineData("partial", false)]
	[InlineData("full", true)]
	[InlineData("full", false)]
	public void Completion_policy_survives_normalization_and_does_not_restore_legacy_money_settings(string rule, bool later)
	{
		var json = JsonSerializer.Serialize(new { payment = new { amount = 500 }, paymentCompletion = new { version = 1, paymentRule = rule, allowCollectionLater = later }, extension = "keep" });
		var normalized = AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment, json);
		var policy = AdmissionPaymentCompletionPolicy.Read(normalized);
		policy.PaymentRule.ShouldBe(rule); policy.AllowCollectionLater.ShouldBe(later);
		using var parsed = JsonDocument.Parse(normalized!);
		parsed.RootElement.TryGetProperty("payment", out _).ShouldBeFalse();
		parsed.RootElement.GetProperty("extension").GetString().ShouldBe("keep");
	}
	[Theory]
	[InlineData("{\"version\":99}")]
	[InlineData("{\"paymentRule\":\"free\"}")]
	[InlineData("{\"allowCollectionLater\":\"false\"}")]
	[InlineData("null")]
	[InlineData("[]")]
	public void Unsupported_or_malformed_policy_is_rejected(string json) =>
		Should.Throw<Exception>(() => AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment, "{\"paymentCompletion\":" + json + "}"));
}
