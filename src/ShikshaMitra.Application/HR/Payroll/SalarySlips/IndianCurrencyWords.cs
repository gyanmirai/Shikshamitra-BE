using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class IndianCurrencyWords
{
	public static string Format(decimal amount)
	{
		var rupees = (long) Math.Floor(Math.Abs(amount));
		var paise = (int) Math.Round((Math.Abs(amount) - rupees) * 100, MidpointRounding.AwayFromZero);
		if (paise == 100)
		{
			rupees += 1;
			paise = 0;
		}

		var words = $"{NumberToWords(rupees)} rupees";
		if (paise > 0)
		{
			words += $" and {NumberToWords(paise)} paise";
		}

		return $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words)} only";
	}

	private static string NumberToWords(long number)
	{
		if (number == 0)
		{
			return "zero";
		}

		string[] units =
		[
			"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
			"eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
			"eighteen", "nineteen"
		];
		string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

		static string TwoDigits(long value, string[] localUnits, string[] localTens)
		{
			if (value < 20)
			{
				return localUnits[(int) value];
			}

			return localTens[(int) (value / 10)] + (value % 10 > 0 ? " " + localUnits[(int) (value % 10)] : "");
		}

		static string ThreeDigits(long value, string[] localUnits, string[] localTens)
		{
			var hundred = value / 100;
			var rest = value % 100;
			var result = hundred > 0 ? localUnits[(int) hundred] + " hundred" : "";
			if (rest > 0)
			{
				result += (result.Length > 0 ? " " : "") + TwoDigits(rest, localUnits, localTens);
			}

			return result;
		}

		var parts = new List<string>();
		var crore = number / 10000000;
		number %= 10000000;
		var lakh = number / 100000;
		number %= 100000;
		var thousand = number / 1000;
		number %= 1000;

		if (crore > 0)
		{
			parts.Add($"{NumberToWords(crore)} crore");
		}

		if (lakh > 0)
		{
			parts.Add($"{NumberToWords(lakh)} lakh");
		}

		if (thousand > 0)
		{
			parts.Add($"{NumberToWords(thousand)} thousand");
		}

		if (number > 0)
		{
			parts.Add(ThreeDigits(number, units, tens));
		}

		return string.Join(" ", parts);
	}
}
