using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmChannelSettingsSerializer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static string SerializeSettings(IReadOnlyList<AdmissionCrmChannelSettingDto>? settings)
	{
		var normalized = NormalizeSettings(settings).ToList();
		var json = JsonSerializer.Serialize(normalized, JsonOptions);
		if (json.Length > AdmissionCrmConsts.MaxRoutingRulesJsonLength)
		{
			throw new BusinessException("AdmissionCrm:ChannelSettingsTooLarge");
		}

		return json;
	}

	public static List<AdmissionCrmChannelSettingDto> DeserializeSettings(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson))
		{
			return CreateDefaultSettings();
		}

		try
		{
			return NormalizeSettings(
				JsonSerializer.Deserialize<List<AdmissionCrmChannelSettingDto>>(settingsJson, JsonOptions))
				.ToList();
		}
		catch (JsonException)
		{
			return CreateDefaultSettings();
		}
	}

	public static AdmissionCrmChannelSettingDto GetChannelSetting(
		string? settingsJson,
		AdmissionInquiryChannel channel)
	{
		if (!Enum.IsDefined(channel))
		{
			throw new BusinessException("AdmissionCrm:InvalidChannelSetting")
				.WithData("Channel", channel);
		}

		return DeserializeSettings(settingsJson).Single(x => x.Channel == channel);
	}

	private static IEnumerable<AdmissionCrmChannelSettingDto> NormalizeSettings(
		IReadOnlyList<AdmissionCrmChannelSettingDto>? settings)
	{
		var validChannels = Enum.GetValues<AdmissionInquiryChannel>().ToList();
		var provided = (settings ?? [])
			.Select(NormalizeSetting)
			.ToList();
		var duplicateChannel = provided
			.GroupBy(x => x.Channel)
			.FirstOrDefault(x => x.Count() > 1)
			?.Key;
		if (duplicateChannel.HasValue)
		{
			throw new BusinessException("AdmissionCrm:DuplicateChannelSetting")
				.WithData("Channel", duplicateChannel.Value);
		}

		var invalid = provided.FirstOrDefault(x => !validChannels.Contains(x.Channel));
		if (invalid != null)
		{
			throw new BusinessException("AdmissionCrm:InvalidChannelSetting")
				.WithData("Channel", invalid.Channel);
		}

		var providedByChannel = provided.ToDictionary(x => x.Channel);
		return validChannels
			.Select((channel, index) =>
			{
				if (!providedByChannel.TryGetValue(channel, out var setting))
				{
					return CreateDefaultSetting(channel, index);
				}

				if (setting.DisplayOrder <= 0)
				{
					setting.DisplayOrder = (index + 1) * 10;
				}

				return setting;
			})
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Channel);
	}

	private static AdmissionCrmChannelSettingDto NormalizeSetting(AdmissionCrmChannelSettingDto setting)
	{
		return new AdmissionCrmChannelSettingDto
		{
			Channel = setting.Channel,
			IsEnabled = setting.IsEnabled,
			DefaultInquirySourceId = setting.DefaultInquirySourceId,
			DefaultFormTemplateId = setting.DefaultFormTemplateId,
			DefaultOwnerUserId = setting.DefaultOwnerUserId,
			DefaultAcknowledgementTemplateId = setting.DefaultAcknowledgementTemplateId,
			DisplayOrder = setting.DisplayOrder
		};
	}

	private static List<AdmissionCrmChannelSettingDto> CreateDefaultSettings()
	{
		return Enum.GetValues<AdmissionInquiryChannel>()
			.Select(CreateDefaultSetting)
			.ToList();
	}

	private static AdmissionCrmChannelSettingDto CreateDefaultSetting(
		AdmissionInquiryChannel channel,
		int index)
	{
		return new AdmissionCrmChannelSettingDto
		{
			Channel = channel,
			IsEnabled = true,
			DisplayOrder = (index + 1) * 10
		};
	}
}
