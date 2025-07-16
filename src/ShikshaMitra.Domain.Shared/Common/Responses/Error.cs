namespace ShikshaMitra.Common.Responses;

public class Error
{
	public Error(string code, string message)
	{
		Code = code;
		Message = message;
	}

	public string Code { get; }
	public string Message { get; }

	public static readonly Error None = new(string.Empty, string.Empty);

	public static Error Create(string code, string message) => new(code, message);

	public static implicit operator Result(Error error) => Result.Failure(error);
}