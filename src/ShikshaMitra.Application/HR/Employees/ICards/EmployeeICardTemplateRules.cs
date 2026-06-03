using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using static ShikshaMitra.HR.Employees.ICards.EmployeeICardTemplateFieldCatalog;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardTemplateRules
{
	private const long MaxAssetSize = 2 * 1024 * 1024;

	private static readonly HashSet<string> AllowedAssetTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"image/png",
		"image/jpeg",
		"image/jpg"
	};

	private static readonly HashSet<string> AllowedAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg"
	};

	public static void ValidateFieldsJson(string fieldsJson)
	{
		ParseFieldsJson(fieldsJson);
	}

	public static void ValidateTemplateInput(EmployeeICardTemplateRequest input, Guid? tenantId)
	{
		ValidateTemplateAssetReferences(tenantId, input.LogoBlobName, input.BackgroundImageBlobName);
		ValidateColor(input.ThemeColor);
		ValidateColor(input.BackgroundColor);
		EmployeeICardLayoutSchema.Validate(input.LayoutJson, input.PrintSettingsJson);
	}

	public static void ValidateTemplateAssetReferences(Guid? tenantId, params string?[] blobNames)
	{
		foreach (var blobName in blobNames)
		{
			if (!string.IsNullOrWhiteSpace(blobName))
			{
				ValidateAssetBlobName(blobName, tenantId);
			}
		}
	}

	public static Dictionary<string, bool> ParseFieldsJson(string fieldsJson)
	{
		try
		{
			using var document = JsonDocument.Parse(fieldsJson);
			if (document.RootElement.ValueKind is not JsonValueKind.Array)
			{
				throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
			}

			var fields = BuildDisabledFields();
			foreach (var field in document.RootElement.EnumerateArray())
			{
				if (!field.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()))
				{
					throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
				}

				var fieldName = name.GetString()!;
				if (!SupportedFieldNames.Contains(fieldName))
				{
					throw new BusinessException(HRErrorCodes.EmployeeICardFieldSchemaUnsupported);
				}

				var enabled = !field.TryGetProperty("enabled", out var enabledValue) ||
					enabledValue.ValueKind != JsonValueKind.False;
				fields[fieldName] = enabled;
			}

			foreach (var mandatoryField in MandatoryFieldNames)
			{
				fields[mandatoryField] = true;
			}

			return fields;
		}
		catch (JsonException)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	public static void ValidateAsset(string fileName, string contentType, byte[] content)
	{
		var extension = Path.GetExtension(fileName);
		if (string.IsNullOrWhiteSpace(fileName) ||
			content.Length == 0 ||
			content.Length > MaxAssetSize ||
			!AllowedAssetTypes.Contains(contentType) ||
			!AllowedAssetExtensions.Contains(extension) ||
			!HasValidImageSignature(extension, content))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetInvalid);
		}
	}

	public static void ValidateAssetBlobName(string blobName, Guid? tenantId)
	{
		var expectedPrefix = $"employee-icards/assets/{tenantId ?? Guid.Empty}/";
		var extension = Path.GetExtension(blobName);
		if (string.IsNullOrWhiteSpace(blobName) ||
			!blobName.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
			!AllowedAssetExtensions.Contains(extension))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetBlobInvalid);
		}
	}

	private static bool HasValidImageSignature(string extension, byte[] content)
	{
		if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
		{
			return content.Length >= 8 &&
				content[0] == 0x89 &&
				content[1] == 0x50 &&
				content[2] == 0x4E &&
				content[3] == 0x47 &&
				content[4] == 0x0D &&
				content[5] == 0x0A &&
				content[6] == 0x1A &&
				content[7] == 0x0A;
		}

		return content.Length >= 3 &&
			content[0] == 0xFF &&
			content[1] == 0xD8 &&
			content[2] == 0xFF;
	}

	private static void ValidateColor(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		var color = value.Trim();
		if (!color.StartsWith("#", StringComparison.Ordinal))
		{
			color = $"#{color}";
		}

		var hex = color[1..];
		if (hex.Length is not 6 || hex.Any(character => !Uri.IsHexDigit(character)))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

}
