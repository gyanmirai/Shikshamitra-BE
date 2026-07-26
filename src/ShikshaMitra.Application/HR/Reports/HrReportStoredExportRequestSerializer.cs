using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

internal sealed class HrReportStoredExportRequest
{
	public HrReportRequestDto? Request { get; set; }
	public HrReportExportScopeSnapshot? Scope { get; set; }
	public Guid GenerationId { get; set; }
}

internal static class HrReportStoredExportRequestSerializer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public static string Serialize(
		HrReportRequestDto request,
		HrReportExportScopeSnapshot scope,
		Guid generationId) =>
		JsonSerializer.Serialize(new HrReportStoredExportRequest
		{
			Request = request,
			Scope = scope,
			GenerationId = generationId
		}, JsonOptions);

	public static HrReportRequestDto? DeserializeRequest(string requestJson)
	{
		try
		{
			var storedRequest = JsonSerializer.Deserialize<HrReportStoredExportRequest>(requestJson, JsonOptions);
			if (storedRequest?.Request != null && storedRequest.Scope != null)
			{
				return storedRequest.Request;
			}
		}
		catch (JsonException)
		{
			// Fall through to the earlier direct-request payload.
		}

		try
		{
			return JsonSerializer.Deserialize<HrReportRequestDto>(requestJson, JsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static HrReportExportScopeSnapshot? DeserializeScope(string requestJson)
	{
		try
		{
			var storedRequest = JsonSerializer.Deserialize<HrReportStoredExportRequest>(requestJson, JsonOptions);
			var storedScope = storedRequest?.Scope;
			if (storedRequest?.Request == null ||
				storedScope?.Version != HrReportExportScopeSnapshot.CurrentVersion ||
				storedScope.MediumStageIds == null)
			{
				return null;
			}

			storedScope.MediumStageIds = storedScope.MediumStageIds
				.Where(x => x != Guid.Empty)
				.Distinct()
				.OrderBy(x => x)
				.ToList();
			return storedScope;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static Guid? DeserializeGenerationId(string requestJson)
	{
		try
		{
			var storedRequest = JsonSerializer.Deserialize<HrReportStoredExportRequest>(requestJson, JsonOptions);
			return storedRequest?.Request != null &&
				storedRequest.Scope != null &&
				storedRequest.GenerationId != Guid.Empty
					? storedRequest.GenerationId
					: null;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
