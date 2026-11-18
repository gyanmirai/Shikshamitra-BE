using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationRuleConfigurationCodec : ITransientDependency
{
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
		PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		RespectRequiredConstructorParameters = true, MaxDepth = 16
	};
	public string Serialize(NotificationRuleRuntimeConfiguration configuration, NotificationEventDefinition definition)
	{
		Validate(configuration, definition);
		var json = JsonSerializer.Serialize(configuration, Options);
		Require(json.Length <= 65536); return json;
	}
	public NotificationRuleRuntimeConfiguration Deserialize(string json, NotificationEventDefinition definition)
	{
		Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 65536);
		try
		{
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
			CheckUniqueMembers(document.RootElement);
			var result = JsonSerializer.Deserialize<NotificationRuleRuntimeConfiguration>(json, Options);
			Validate(result, definition); return result!;
		}
		catch (JsonException) { throw Invalid(); }
	}
	private static void Validate(NotificationRuleRuntimeConfiguration? config, NotificationEventDefinition definition)
	{
		if (config == null || definition == null || config.AdditionalGuardianRelationships == null ||
			config.StaffAudienceSelectors == null || config.Channels == null || config.TemplateIds == null || config.Timing == null)
			throw Invalid();
		Require(config.SchemaVersion == 1 && Enum.IsDefined(config.GuardianMode));
		Require(config.AdditionalGuardianRelationships.Count <= 3 &&
			config.AdditionalGuardianRelationships.Distinct(StringComparer.Ordinal).Count() == config.AdditionalGuardianRelationships.Count &&
			config.AdditionalGuardianRelationships.All(x => x is "father" or "mother" or "guardian"));
		Require(config.GuardianMode == NotificationGuardianMode.PrimaryAndSelected
			? config.AdditionalGuardianRelationships.Count > 0 && definition.RecipientSelectors.Contains("AdditionalGuardians")
			: config.AdditionalGuardianRelationships.Count == 0);
		Require(!config.IncludeStudentAccount || definition.RecipientSelectors.Contains("StudentAccount"));
		Require(config.StaffAudienceSelectors.Count <= definition.RecipientSelectors.Count &&
			config.StaffAudienceSelectors.Distinct(StringComparer.Ordinal).Count() == config.StaffAudienceSelectors.Count &&
			config.StaffAudienceSelectors.All(x => x is not ("PrimaryGuardian" or "AdditionalGuardians" or "StudentAccount") && definition.RecipientSelectors.Contains(x)));
		Require(definition.RecipientSelectors.Contains("PrimaryGuardian") || config.IncludeStudentAccount || config.StaffAudienceSelectors.Count > 0);
		Require(config.Channels.Count is > 0 and <= 4 && config.Channels.Contains(CommunicationChannelType.InApp) &&
			config.Channels.Distinct().Count() == config.Channels.Count && config.Channels.All(definition.SupportedChannels.Contains));
		Require(config.TemplateIds.Count is > 0 and <= 256 && config.TemplateIds.All(x => x != Guid.Empty) &&
			config.TemplateIds.Distinct().Count() == config.TemplateIds.Count);
		Require(config.FallbackLanguage is { Length: > 0 and <= 16 } && Regex.IsMatch(config.FallbackLanguage,
			"\\A[a-z]{2,3}(?:-[a-z0-9]{2,8})*\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
		var timing = config.Timing;
		Require(timing.DelayMinutes is >= 0 and <= 10080 && timing.ReminderSchoolDaysBefore is >= 0 and <= 365);
		Require(!timing.QuietHoursEnabled || timing.QuietStart != timing.QuietEnd);
		Require(!timing.BypassQuietHours || definition.AllowsQuietHoursBypass);
	}
	private static void CheckUniqueMembers(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (var property in element.EnumerateObject()) { Require(names.Add(property.Name)); CheckUniqueMembers(property.Value); }
		}
		else if (element.ValueKind == JsonValueKind.Array)
			foreach (var child in element.EnumerateArray()) CheckUniqueMembers(child);
	}
	private static void Require(bool condition) { if (!condition) throw Invalid(); }
	private static BusinessException Invalid() => new(NotificationErrorCodes.RuleConfigurationInvalid);
}
