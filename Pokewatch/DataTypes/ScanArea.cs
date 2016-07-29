using System;
using Pokewatch.Datatypes;

namespace Pokewatch.DataTypes
{
	internal sealed class ScanArea : IEquatable<ScanArea>
	{
		public string Name { get; set; }
		public string Prefix { get; set; }
		public string Suffix { get; set; }
		public Location Location { get; set; }

		public bool Equals(ScanArea other)
		{
			return Name.Equals(other.Name) && Prefix.Equals(other.Prefix) && Suffix.Equals(other.Suffix) && Location.Equals(other.Location);
		}
	}
}
