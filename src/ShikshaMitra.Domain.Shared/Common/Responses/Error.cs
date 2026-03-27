using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Common.Responses;

public class Error
{
	public Error(string code, string message, IReadOnlyDictionary<string, string>? fields = null)
	{
		Code = code;
		Message = message;
		Fields = fields;
	}

	public string Code { get; }
	public string Message { get; }

	/// <summary>Per-property validation messages when available (HTTP 400 validation flows).</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyDictionary<string, string>? Fields { get; }

	public static readonly Error None = new(string.Empty, string.Empty);

	public static Error Create(string code, string message, IReadOnlyDictionary<string, string>? fields = null) =>
		new(code, message, fields);
}
