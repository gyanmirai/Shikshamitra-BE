using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WhatsAppTemplateParameter(string Component, int Position, string VariableKey);

internal sealed record WhatsAppCompiledTemplate(string? Header, string Body, string ProviderLanguage,
	IReadOnlyList<WhatsAppTemplateParameter> Parameters);

[RemoteService(false)]
public sealed class WhatsAppTemplateCompiler(NotificationTemplateVariableValidator validator) : ITransientDependency
{
	private sealed record ParameterSnapshot(int SchemaVersion, IReadOnlyList<WhatsAppTemplateParameter> Parameters);

	internal WhatsAppCompiledTemplate Compile(CommunicationTemplateRevision revision)
	{
		if (revision.Channel != CommunicationChannelType.WhatsApp || !revision.IsPublished || revision.IsDeleted ||
			validator.ValidatePublication(revision) != null) throw Invalid();
		var providerLanguage = CultureInfo.GetCultureInfo(revision.LanguageCode).Name.Replace('-', '_');
		if (!WhatsAppProviderClient.ValidLanguage(providerLanguage)) throw Invalid();
		var parameters = new List<WhatsAppTemplateParameter>();
		var header = string.IsNullOrWhiteSpace(revision.Subject) ? null : NumberVariables("header", revision.Subject, parameters);
		var body = NumberVariables("body", revision.Body, parameters);
		return new(header, body, providerLanguage, parameters.ToArray());
	}

	internal string EncodeParameters(WhatsAppCompiledTemplate template) =>
		NotificationRuntimeJson.Serialize(new ParameterSnapshot(1, template.Parameters), NotificationErrorCodes.TemplateInvalid);

	internal IReadOnlyList<WhatsAppTextComponent> Bind(CommunicationTemplateRevision revision, string storedParameters,
		IReadOnlyDictionary<string, NotificationVariableValueDto> variables)
	{
		var template = Compile(revision);
		var snapshot = NotificationRuntimeJson.Deserialize<ParameterSnapshot>(storedParameters, NotificationErrorCodes.TemplateInvalid);
		if (snapshot.SchemaVersion != 1 || snapshot.Parameters == null || !snapshot.Parameters.SequenceEqual(template.Parameters)) throw Invalid();
		var error = validator.ValidateRender(new(revision.Id, revision.EventKey, revision.SchemaVersion, revision.RecipientKind,
			revision.Channel, revision.LanguageCode, variables), revision);
		if (error != null) throw new BusinessException(error);
		var culture = CultureInfo.GetCultureInfo(revision.LanguageCode);
		return template.Parameters.GroupBy(x => x.Component).Select(group => new WhatsAppTextComponent(group.Key,
			group.Select(x => NotificationTemplateVariableValidator.Format(variables[x.VariableKey], culture)).ToArray())).ToArray();
	}

	private static string NumberVariables(string component, string content, List<WhatsAppTemplateParameter> parameters)
	{
		var position = 0;
		return NotificationTemplateVariableValidator.Placeholders().Replace(content, match => {
			position++;
			if (position > WhatsAppProviderClient.MaximumTextParameters) throw Invalid();
			parameters.Add(new(component, position, match.Groups[1].Value));
			return "{{" + position.ToString(CultureInfo.InvariantCulture) + "}}";
		});
	}

	private static BusinessException Invalid() => new(NotificationErrorCodes.TemplateInvalid);
}
