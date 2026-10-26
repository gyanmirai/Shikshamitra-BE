using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Finance.Mobile;

public static class MobileFinanceCommandRules
{
	public static decimal ParseMoney(string value)
	{
		if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, @"^(0|[1-9][0-9]{0,10})\.[0-9]{2}$") ||
			!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
			throw new BusinessException(FamilyFinanceErrorCodes.InvalidAmount);
		return amount;
	}

	public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
	public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
	public static Guid ReadMarkerId(Guid userId, Guid eventId) => StableId($"family-read:{userId:N}:{eventId:N}");
	public static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

	public static void RequireOwner(Guid child, Guid year, Guid actor, Guid actualChild, Guid actualYear, Guid? actualActor)
	{
		if (child != actualChild || year != actualYear || actor != actualActor)
			throw new AbpAuthorizationException();
	}

	public static void RequireFingerprint(string current, string? submitted)
	{
		if (string.IsNullOrWhiteSpace(submitted) || !string.Equals(current, submitted, StringComparison.Ordinal))
			throw new BusinessException(FamilyFinanceErrorCodes.PreviewChanged);
	}

	public static string RequireKey(string? key)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key != key.Trim())
			throw new BusinessException(FamilyFinanceErrorCodes.IdempotencyKeyRequired);
		return key;
	}
}
