using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Common;

/// <summary>
/// Security utility methods for input sanitization and validation
/// Prevents XSS, SQL injection, and other input-based attacks
/// </summary>
public static class SecurityHelper
{
	/// <summary>
	/// Checks if a string contains potentially dangerous characters for SQL injection
	/// Note: This is a defense-in-depth measure. Primary protection is parameterized queries.
	/// </summary>
	public static bool ContainsDangerousCharacters(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		// Check for common SQL injection patterns
		var dangerousPatterns = new[]
		{
			"--",        // SQL comment
			";",         // SQL statement separator
			"/*",        // SQL block comment start
			"*/",        // SQL block comment end
			"xp_",       // SQL Server extended procedure
			"sp_",       // SQL Server stored procedure
			"exec",      // SQL execute
			"execute",   // SQL execute
			"script",    // JavaScript
			"javascript:", // JavaScript protocol
			"<script",   // HTML script tag
			"onerror",   // JavaScript event
			"onload",    // JavaScript event
		};

		var lowerInput = input.ToLowerInvariant();
		return dangerousPatterns.Any(pattern => lowerInput.Contains(pattern));
	}

	/// <summary>
	/// Sanitizes a filename to prevent path traversal attacks
	/// </summary>
	public static string SanitizeFileName(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new ArgumentException("Filename cannot be empty", nameof(fileName));
		}

		// Remove any path information (path traversal prevention)
		fileName = System.IO.Path.GetFileName(fileName);

		// Remove or replace dangerous characters
		var invalidChars = System.IO.Path.GetInvalidFileNameChars();
		fileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

		// Remove potentially dangerous patterns
		fileName = fileName.Replace("..", "");  // Path traversal
		fileName = fileName.Replace("./", "");  // Path traversal
		fileName = fileName.Replace("../", ""); // Path traversal

		// Limit length
		if (fileName.Length > 255)
		{
			var extension = System.IO.Path.GetExtension(fileName);
			var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
			fileName = nameWithoutExt.Substring(0, 255 - extension.Length) + extension;
		}

		return fileName;
	}

	/// <summary>
	/// Validates that a GUID is not empty (prevents null/empty ID attacks)
	/// </summary>
	public static void ValidateGuid(Guid id, string parameterName)
	{
		if (id == Guid.Empty)
		{
			throw new ArgumentException($"{parameterName} cannot be empty GUID", parameterName);
		}
	}

	/// <summary>
	/// Checks if an email address is valid format
	/// </summary>
	public static bool IsValidEmail(string? email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return false;
		}

		try
		{
			var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
			return regex.IsMatch(email);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Validates phone number format (India-specific, can be extended)
	/// </summary>
	public static bool IsValidPhoneNumber(string? phoneNumber)
	{
		if (string.IsNullOrWhiteSpace(phoneNumber))
		{
			return false;
		}

		// Allow: +91-9876543210, 9876543210, +919876543210, etc.
		var regex = new Regex(@"^\+?[0-9\s\-()]{10,20}$");
		return regex.IsMatch(phoneNumber);
	}

	/// <summary>
	/// Sanitizes search input to prevent LIKE injection in SQL
	/// </summary>
	public static string SanitizeSearchTerm(string? searchTerm)
	{
		if (string.IsNullOrWhiteSpace(searchTerm))
		{
			return string.Empty;
		}

		// Escape LIKE wildcards
		searchTerm = searchTerm.Replace("%", "[%]");
		searchTerm = searchTerm.Replace("_", "[_]");
		searchTerm = searchTerm.Replace("[", "[[]");

		// Remove dangerous characters
		searchTerm = searchTerm.Replace("--", "");
		searchTerm = searchTerm.Replace(";", "");
		searchTerm = searchTerm.Replace("'", "");
		searchTerm = searchTerm.Replace("\"", "");

		// Limit length
		if (searchTerm.Length > 100)
		{
			searchTerm = searchTerm.Substring(0, 100);
		}

		return searchTerm;
	}

	/// <summary>
	/// Validates that pagination parameters are within safe limits
	/// </summary>
	public static (int skip, int take) ValidatePaginationParameters(int page, int pageSize, int maxPageSize = 50)
	{
		// Ensure positive values
		page = Math.Max(1, page);
		pageSize = Math.Max(1, Math.Min(pageSize, maxPageSize));

		// Calculate skip/take
		var skip = (page - 1) * pageSize;
		var take = pageSize;

		// Prevent integer overflow
		if (skip < 0 || skip > int.MaxValue - take)
		{
			skip = 0;
			take = maxPageSize;
		}

		return (skip, take);
	}
}
