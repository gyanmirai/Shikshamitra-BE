namespace ShikshaMitra.Common;

public class Country
{
	public string Name { get; set; }

	public string IsoCode { get; set; }


	private Country()
	{
		// Preserve deferred null defaults until materialization populates these properties.
		Name = null!;
		IsoCode = null!;
	}

	public Country(string name, string isoCode)
	{
		Name = name;
		IsoCode = isoCode;
	}
}
