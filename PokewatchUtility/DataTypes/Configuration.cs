using System.Collections.Generic;
using POGOProtos.Enums;

namespace PokewatchUtility.DataTypes
{
	public sealed class Configuration
	{
		//PokemonGo Accounts
		public List<PoGoAccount> PoGoAccounts { get; set; }

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
		public bool Pokevision { get; set; }

		//Pokemon Name Overrides
		public List<PokemonOverride> PokemonOverrides { get; set; } 
	}
}
