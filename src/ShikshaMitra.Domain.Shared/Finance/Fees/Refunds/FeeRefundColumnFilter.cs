using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>Read-only worklist filters. Alternatives within a column use OR; columns use AND.</summary>
public sealed class FeeRefundColumnFilter
{
    public string Column { get; set; } = "";
    public string Type { get; set; } = "";
    public JsonElement Value { get; set; }

    public static List<FeeRefundColumnFilter> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            if (json.Length > 16000) throw new FormatException();
            var filters = JsonSerializer.Deserialize<List<FeeRefundColumnFilter>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new FormatException();
            if (filters.Count > 30 || filters.Any(f => f == null || !f.IsValid())) throw new FormatException();
            return filters;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new AbpValidationException("Invalid refund column filters.",
                [new ValidationResult("Choose a supported column, operator and value.", ["Filters"])]);
        }
    }

    private bool IsValid()
    {
        var value = Value.ToString();
        if (value.Length > 300) return false;
        return Column switch
        {
            "_voucherNumber" or "name" or "classDivision" or "_receiptNumber" or "refundMethod" =>
                Value.ValueKind == JsonValueKind.String && Type is "contains" or "equals" or "startsWith" or "endsWith",
            "refundAmount" => Type is "equals" or "greaterThan" or "lessThan" &&
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "status" => Type == "equals" && int.TryParse(value, out var status) &&
                Enum.IsDefined(typeof(FeeRefundStatus), status),
            "requestDate" => Type is "equals" or "after" or "before" &&
                DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }
}
