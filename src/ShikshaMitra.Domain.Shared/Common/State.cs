namespace ShikshaMitra.Common;

public class State
{
	public string Name { get; set; }

	public string IsoCode { get; set; }

	private State()
	{
		// Preserve deferred null defaults until materialization populates these properties.
		Name = null!;
		IsoCode = null!;
	}
	public State(string name, string isoCode)
	{
		Name = name;
		IsoCode = isoCode;
	}
}
