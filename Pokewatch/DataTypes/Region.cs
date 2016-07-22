using System;
using System.Collections.Generic;
using System.Linq;

namespace Pokewatch.Datatypes
{
	internal sealed class Region : IEquatable<Region>
	{
		public string Name { get; set; }
		public List<Location> Locations { get; set; }

		public bool Equals(Region other)
		{
			return Name.Equals(other.Name) && Locations.SequenceEqual(other.Locations);
		}
	}
}
