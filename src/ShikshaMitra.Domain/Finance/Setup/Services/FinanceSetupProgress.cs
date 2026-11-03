using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Setup;

public static class FinanceSetupProgress
{
	public const string Property = "FinanceSetupGuideProgressV1";
	public sealed class Document
	{
		[JsonRequired]
		public int Version { get; set; } = 1;
		[JsonRequired]
		public string CurrentStageKey { get; set; } = "accounts";
		[JsonRequired]
		public string? CurrentSettingKey { get; set; } = "income-categories";
		[JsonRequired]
		public string[] DeferredSettingKeys { get; set; } = [];
		[JsonRequired]
		public DateTime? ReviewedAt { get; set; }
		[JsonRequired]
		public Guid? ReviewedBy { get; set; }
	}

	public static Document Read(AcademicYear year)
	{
		if (!year.ExtraProperties.TryGetValue(Property, out var value)) return new();
		if (value is not string json || json.Length > 4096) throw new BusinessException(FinanceSetupKeys.InvalidStoredState);
		try
		{
			var document = JsonSerializer.Deserialize<Document>(json);
			if (document != null && document.Version == 1 && IsValid(document.CurrentStageKey, document.CurrentSettingKey, document.DeferredSettingKeys) &&
				document.ReviewedAt.HasValue == document.ReviewedBy.HasValue && document.ReviewedBy != Guid.Empty) return document;
		}
		catch (JsonException) { }
		throw new BusinessException(FinanceSetupKeys.InvalidStoredState);
	}

	public static bool IsValid(string? stage, string? setting, string[]? deferred) =>
		FinanceSetupKeys.IsStage(stage) && (setting == null || FinanceSetupKeys.SettingStages.TryGetValue(setting, out var owner) && owner == stage) &&
		deferred != null && deferred.Length <= 12 && deferred.Distinct(StringComparer.Ordinal).Count() == deferred.Length &&
		deferred.All(key => key != null && FinanceSetupKeys.SettingStages.ContainsKey(key));

	public static void Save(AcademicYear year, string stage, string? setting, string[] deferred, bool finishReview, Guid actorId, DateTime now)
	{
		if (!IsValid(stage, setting, deferred) || actorId == Guid.Empty || finishReview && stage != "review")
			throw new BusinessException(FinanceSetupKeys.Invalid);
		var document = Read(year);
		if (document.CurrentStageKey != stage || document.CurrentSettingKey != setting ||
			!document.DeferredSettingKeys.Order(StringComparer.Ordinal).SequenceEqual(deferred.Order(StringComparer.Ordinal)))
		{
			document.ReviewedAt = null;
			document.ReviewedBy = null;
		}
		document.CurrentStageKey = stage;
		document.CurrentSettingKey = setting;
		document.DeferredSettingKeys = deferred;
		if (finishReview) { document.ReviewedAt ??= now; document.ReviewedBy ??= actorId; }
		year.SetProperty(Property, JsonSerializer.Serialize(document));
	}
}
