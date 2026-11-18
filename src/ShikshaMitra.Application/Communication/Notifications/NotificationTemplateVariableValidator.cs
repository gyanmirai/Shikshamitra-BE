using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed partial class NotificationTemplateVariableValidator(IEnumerable<INotificationEventDefinitionProvider> schemas,
	IConfiguration configuration) : ITransientDependency
{
	public NotificationEventDefinition GetDefinition(string eventKey)
	{
		var matches = schemas.SelectMany(x => x.GetAll()).Where(x => x.EventKey == eventKey).ToArray();
		return matches.Length == 1 ? matches[0] : throw new Volo.Abp.BusinessException(NotificationErrorCodes.EventUnknown);
	}

	public string? ValidatePublication(CommunicationTemplateRevision revision)
	{
		var definitions = schemas.SelectMany(x => x.GetAll()).Where(x => x.EventKey == revision.EventKey).ToArray();
		if (definitions.Length != 1) return NotificationErrorCodes.TemplateInvalid;
		var definition = definitions[0];
		if (definition.SchemaVersion != revision.SchemaVersion || !definition.SupportedChannels.Contains(revision.Channel) ||
			!RecipientSupported(revision.RecipientKind, definition.RecipientSelectors) ||
			!ValidLanguage(revision.LanguageCode) || !ValidSyntax(revision.Subject, definition) || !ValidSyntax(revision.Body, definition) ||
			!WithinLimits(revision.Channel, revision.Subject, revision.Body))
			return NotificationErrorCodes.TemplateInvalid;
		return null;
	}

	public string? ValidateRender(NotificationRenderInput input, CommunicationTemplateRevision revision)
	{
		var error = ValidatePublication(revision);
		if (error != null) return error;
		if (input.EventKey != revision.EventKey || input.SchemaVersion != revision.SchemaVersion || input.RecipientKind != revision.RecipientKind ||
			input.Channel != revision.Channel || !string.Equals(input.LanguageCode, revision.LanguageCode, StringComparison.OrdinalIgnoreCase) || input.Variables == null)
			return NotificationErrorCodes.TemplateInvalid;
		var definition = schemas.SelectMany(x => x.GetAll()).Single(x => x.EventKey == input.EventKey);
		var fields = definition.Variables.ToDictionary(x => x.Key, StringComparer.Ordinal);
		var required = definition.Variables.Where(x => x.Required).Select(x => x.Key)
			.Concat(Placeholders().Matches(revision.Subject + "\n" + revision.Body).Select(x => x.Groups[1].Value)).Distinct(StringComparer.Ordinal);
		if (required.Any(key => !input.Variables.TryGetValue(key, out var value) || value == null || string.IsNullOrWhiteSpace(value.Value)))
			return NotificationErrorCodes.RequiredVariableMissing;
		foreach (var (key, value) in input.Variables)
		{
			if (!fields.TryGetValue(key, out var field) || value == null || value.Type != field.Type || !ValidValue(value))
				return NotificationErrorCodes.TemplateInvalid;
		}
		return null;
	}

	public string Substitute(string template, NotificationRenderInput input, bool encodeHtml = false) => Placeholders().Replace(template, match =>
	{
		var value = input.Variables[match.Groups[1].Value];
		var text = Format(value, CultureInfo.GetCultureInfo(input.LanguageCode));
		return encodeHtml ? WebUtility.HtmlEncode(text) : text;
	});

	public bool IsAllowedLink(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsControl) || value.Contains('\\') ||
			!Uri.TryCreate(value, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps || link.UserInfo.Length != 0 ||
			!Uri.TryCreate(configuration["App:ClientUrl"], UriKind.Absolute, out var school) || school.Scheme != Uri.UriSchemeHttps || school.UserInfo.Length != 0)
			return false;
		return string.Equals(link.GetLeftPart(UriPartial.Authority), school.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
	}

	public static bool WithinLimits(CommunicationChannelType channel, string subject, string body)
	{
		if (subject.Any(char.IsControl) || string.IsNullOrWhiteSpace(body)) return false;
		var (titleLimit, bodyLimit) = channel switch {
			CommunicationChannelType.WebPush => (80, 240),
			CommunicationChannelType.WhatsApp => (60, 1024),
			CommunicationChannelType.InApp => (256, 4096),
			CommunicationChannelType.Email => (256, 65536),
			_ => (0, 0)
		};
		return subject.Length <= titleLimit && body.Length <= bodyLimit;
	}

	private bool ValidValue(NotificationVariableValueDto value)
	{
		if (value.Value == null) return true;
		if (value.Value.Length > 4096 || value.Value.Contains('\0')) return false;
		return value.Type switch {
			NotificationVariableType.Text => true,
			NotificationVariableType.Link => IsAllowedLink(value.Value),
			NotificationVariableType.Date => DateOnly.TryParseExact(value.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
			NotificationVariableType.DateTime => OffsetSuffix().IsMatch(value.Value) && DateTimeOffset.TryParse(value.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
			NotificationVariableType.Number => decimal.TryParse(value.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
			_ => false
		};
	}

	private static string Format(NotificationVariableValueDto value, CultureInfo culture) => value.Type switch {
		NotificationVariableType.Date => DateOnly.ParseExact(value.Value!, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd MMM yyyy", culture),
		NotificationVariableType.DateTime => DateTimeOffset.Parse(value.Value!, CultureInfo.InvariantCulture).ToString("dd MMM yyyy, HH:mm", culture),
		NotificationVariableType.Number => decimal.Parse(value.Value!, CultureInfo.InvariantCulture).ToString(culture),
		_ => value.Value ?? string.Empty
	};

	private static bool RecipientSupported(string kind, IReadOnlyList<string> selectors) => kind switch {
		"Guardian" => selectors.Any(x => x is "PrimaryGuardian" or "AdditionalGuardians"),
		"StudentAccount" => selectors.Contains("StudentAccount"),
		_ => selectors.Contains(kind)
	};

	private static bool ValidLanguage(string language)
	{
		try { return !string.IsNullOrWhiteSpace(language) && CultureInfo.GetCultureInfo(language).Name.Length != 0; }
		catch (CultureNotFoundException) { return false; }
	}

	private static bool ValidSyntax(string text, NotificationEventDefinition definition)
	{
		var matches = Placeholders().Matches(text);
		if (matches.Any(match => !definition.Variables.Any(x => x.Key == match.Groups[1].Value))) return false;
		var literal = Placeholders().Replace(text, string.Empty);
		return !literal.Contains("{{", StringComparison.Ordinal) && !literal.Contains("}}", StringComparison.Ordinal);
	}

	[GeneratedRegex(@"\{\{([a-z][a-z0-9_]*)\}\}", RegexOptions.CultureInvariant)]
	private static partial Regex Placeholders();
	[GeneratedRegex(@"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
	private static partial Regex OffsetSuffix();
}
