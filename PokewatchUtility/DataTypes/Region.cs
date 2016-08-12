using System;
using System.Collections.Generic;
using System.Linq;
using POGOProtos.Enums;

namespace PokewatchUtility.DataTypes
{
	public sealed class Region : IEquatable<Region>
	{
		public string Name { get; set; }
		public string Prefix { get; set; }
		public string Suffix { get; set; }
		public List<Location> Locations { get; set; }
		public List<PokemonId> Exclusions { get; set; }
		public List<PokemonId> Inclusions { get; set; }

		public bool Equals(Region other)
		{
			return Name.Equals(other.Name) && Prefix.Equals(other.Prefix) && Suffix.Equals(other.Suffix) && Locations.SequenceEqual(other.Locations);
		}
	}
}
