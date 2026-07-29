using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmCommunicationProviderSettings_Tests
{
	[Fact]
	public void Read_model_redacts_provider_credentials_and_reports_stored_values()
	{
		var settings = BuildStoredSettings();

		var sanitized = AdmissionCrmCommunicationAppService.SanitizeProviderSettings(settings);

		sanitized.Headers["Authorization"].ShouldBeEmpty();
		sanitized.Parameters["apiKey"].ShouldBeEmpty();
		sanitized.CallbackSecret.ShouldBeNull();
		sanitized.HasStoredHeaderValues.ShouldBeTrue();
		sanitized.HasStoredParameterValues.ShouldBeTrue();
		sanitized.HasCallbackSecret.ShouldBeTrue();
		sanitized.Adapter.ShouldBe("webhook");
		sanitized.CallbackMappings.StatusPath.ShouldBe("event.status");
	}

	[Fact]
	public void Blank_write_only_values_preserve_credentials_and_omitted_keys_remove_them()
	{
		var stored = BuildStoredSettings();
		stored.Headers["X-Remove"] = "remove-me";
		var input = new AdmissionCrmCommunicationProviderSettingsDto
		{
			Adapter = "webhook",
			PayloadMode = "envelope",
			Headers = new Dictionary<string, string>
			{
				["Authorization"] = string.Empty,
				["X-School"] = "main-campus"
			},
			Parameters = new Dictionary<string, string>
			{
				["apiKey"] = string.Empty
			},
			CallbackSecret = " ",
			CallbackMappings = new AdmissionCrmCommunicationCallbackMappingsDto
			{
				StatusPath = "payload.delivery.status"
			}
		};

		var merged = AdmissionCrmCommunicationAppService.MergeProviderSettings(stored, input);

		merged.Headers["Authorization"].ShouldBe("Bearer stored-token");
		merged.Headers["X-School"].ShouldBe("main-campus");
		merged.Headers.ContainsKey("X-Remove").ShouldBeFalse();
		merged.Parameters["apiKey"].ShouldBe("stored-api-key");
		merged.CallbackSecret.ShouldBe("stored-callback-secret");
		merged.PayloadMode.ShouldBe("envelope");
		merged.CallbackMappings.StatusPath.ShouldBe("payload.delivery.status");
	}

	private static AdmissionCrmCommunicationProviderSettingsDto BuildStoredSettings()
	{
		return new AdmissionCrmCommunicationProviderSettingsDto
		{
			Adapter = "webhook",
			PayloadMode = "default",
			Headers = new Dictionary<string, string>
			{
				["Authorization"] = "Bearer stored-token"
			},
			Parameters = new Dictionary<string, string>
			{
				["apiKey"] = "stored-api-key"
			},
			CallbackSecret = "stored-callback-secret",
			CallbackMappings = new AdmissionCrmCommunicationCallbackMappingsDto
			{
				StatusPath = "event.status"
			}
		};
	}
}
