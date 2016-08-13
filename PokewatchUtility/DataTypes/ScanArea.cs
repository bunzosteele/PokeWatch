using System;
using System.Collections.Generic;
using System.Linq;
using POGOProtos.Enums;

namespace PokewatchUtility.DataTypes
{
	public sealed class ScanArea : IEquatable<ScanArea>
	{
		public string Name { get; set; }
		public string Prefix { get; set; }
		public string Suffix { get; set; }
		public Location Location { get; set; }
		public List<PokemonId> Exclusions { get; set; }
		public List<PokemonId> Inclusions { get; set; }

		public bool Equals(ScanArea other)
		{
			return Name.Equals(other.Name)
				&& Prefix.Equals(other.Prefix)
				&& Suffix.Equals(other.Suffix)
				&& Location.Equals(other.Location)
				&& Exclusions.SequenceEqual(other.Exclusions)
				&& Inclusions.SequenceEqual(other.Inclusions);
		}
	}
}
