using System;
using System.Collections.Generic;
using System.Linq;

namespace Pokewatch.Datatypes
{
	internal sealed class Region : IEquatable<Region>
	{
		public string Name { get; set; }
		public string Preface { get; set; }
		public string Suffix { get; set; }
		public List<Location> Locations { get; set; }

		public bool Equals(Region other)
		{
			return Name.Equals(other.Name) && Preface.Equals(other.Preface) && Suffix.Equals(other.Suffix) && Locations.SequenceEqual(other.Locations);
		}
	}
}
