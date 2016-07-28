using System.Collections.Generic;
using Pokewatch.Datatypes;
using POGOProtos.Enums;

namespace Pokewatch.DataTypes
{
	internal sealed class Configuration
	{
		//PokemonTrainerClub Login Info
		public string PTCUsername { get; set; }
		public string PTCPassword { get; set; }

		//Google Account Login Info
		public string GAUsername { get; set; }
		public string GAPassword { get; set; }

		//Twitter OAuth Strings
		public string TwitterConsumerToken { get; set; }
		public string TwitterConsumerSecret { get; set; }
		public string TwitterAccessToken { get; set; }
		public string TwitterAccessSecret { get; set; }

		//Minimum Delay In Seconds Between Finding Pokemon
		public int RateLimit { get; set; }

		//Minimum Seconds Before Pokemon Despawns
		public int MinimumLifeExpectancy { get; set; }

		//Regions To Search
		public List<Region> Regions { get; set; }

		//Pokemon To Ignore
		public List<PokemonId> ExcludedPokemon { get; set; }

		//Secondary Class Of Accepted Pokemon That Ignores RateLimit
		public List<PokemonId> PriorityPokemon { get; set; }

		//Control Tweet Content
		public string RegularTweet { get; set; }
		public string PriorityTweet { get; set; }
		public bool TagPokemon { get; set; }
		public bool TagRegion { get; set; }
		public List<string> CustomTags { get; set; }

		//Pokemon Name Overrides
		public List<PokemonOverride> PokemonOverrides { get; set; } 
	}
}
