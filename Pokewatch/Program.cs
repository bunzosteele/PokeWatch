using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Pokewatch.Datatypes;
using POGOLib.Net;
using POGOLib.Pokemon;
using POGOProtos.Enums;
using POGOProtos.Map.Pokemon;
using Tweetinvi;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Models;
using Location = Pokewatch.Datatypes.Location;

namespace Pokewatch
{
	public class Program
	{
		public static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("[!]Usage: Pokewatch.exe -username -password [-auth]");
				return;
			}

			s_username = args[0];
			s_password = args[1];
			if(args.Length > 2)
				s_auth = args[2];

			if (s_username.IsNullOrEmpty() || s_password.IsNullOrEmpty())
			{
				Console.WriteLine("[-]Username and password must be supplied.");
				return;
			}

			if (PrepareTwitterClient())
			{
				Console.WriteLine("[+]Sucessfully signed in to twitter.");
			}
			else
			{
				Console.WriteLine("[-]Unable to authenticate Twitter account.");
				return;
			}

			if (PrepareClient())
			{
				Console.WriteLine("[+]Sucessfully logged in to PokemonGo.");
			}
			else
			{
				Console.WriteLine("[-]Unable to authenticate Pokemon account.");
				return;
			}

			Search();
		}

		private static void Search()
		{
			Queue<FoundPokemon> tweetedPokemon = new Queue<FoundPokemon>();
			Region previousRegion = null;

			while (true)
			{
				Region region = SelectRegion(previousRegion);
				Console.WriteLine($"[!]Searching Region: {region.Name}");

				bool found = false;
				foreach (Location location in region.Locations)
				{
					SetLocation(location);

					//Wait so we don't clobber api and to let the heartbeat catch up to our new location. (Minimum heartbeat time is 4000ms)
					Thread.Sleep(5000);

					Console.WriteLine("[!]Searching nearby cells.");
					var mapObjects = s_poClient.MapObjects;
					if (s_poClient.MapObjects == null)
					{
						Console.WriteLine("[-]Heartbeat has failed. Terminating Connection.");
						throw new Exception();
					}

					foreach (var mapCell in mapObjects.MapCells)
					{
						foreach (WildPokemon pokemon in mapCell.WildPokemons)
						{
							FoundPokemon foundPokemon = ProcessPokemon(pokemon, tweetedPokemon);

							if (foundPokemon == null)
								continue;
							
							s_twitterClient.PublishTweet(ComposeTweet(foundPokemon, region));
							Console.WriteLine("[+]Tweet published.");

							found = true;
							previousRegion = region;
							tweetedPokemon.Enqueue(foundPokemon);

							if (tweetedPokemon.Count > 10)
								tweetedPokemon.Dequeue();
							break;
						}
						if (found)
							break;
					}
					if (found)
						break;
				}
				Console.WriteLine("[!]Finished Searching " + region.Name);
			}
		}

		//Sign in to PokemonGO
		private static bool PrepareClient()
		{
			var loginProvider = LoginProvider.PokemonTrainerClub;

			if (s_auth == "google")
				loginProvider = LoginProvider.GoogleAuth;

			Console.WriteLine("[!]Using auth provider:" + loginProvider);

			s_poClient = new PoClient(s_username, loginProvider);

			Console.WriteLine("[!]Setting up PoClient for user:" + s_username);

			// Client requires a location be set when signing in.
			SetLocation(Data.Regions.First().Locations.First());

			return s_poClient.Authenticate(s_password);
		}

		//Sign in to Twitter.
		private static bool PrepareTwitterClient()
		{
			Console.WriteLine("[!]Signing in to Twitter.");
			var userCredentials = Auth.CreateCredentials(s_consumerToken, s_consumerSecret, s_accessToken, s_accessSecret);
			ExceptionHandler.SwallowWebExceptions = false;
			try
			{
				s_twitterClient = User.GetAuthenticatedUser(userCredentials);
			}
			catch
			{
				return false;
			}
			return true;
		}

		private static void SetLocation(Location location)
		{
			Console.WriteLine($"[!]Setting location to {location.Latitude},{location.Longitude}");
			s_poClient.SetGpsData(location.Latitude, location.Longitude);
		}

		//Evaluate if a pokemon is worth tweeting about.
		private static FoundPokemon ProcessPokemon(WildPokemon pokemon, Queue<FoundPokemon> alreadyFound)
		{
			FoundPokemon foundPokemon = new FoundPokemon
			{
				Location = new Location { Latitude = pokemon.Latitude, Longitude = pokemon.Longitude},
				Type = pokemon.Pokemon.PokemonType,
				LifeExpectancy = TimeSpan.FromMilliseconds(pokemon.TimeTillHiddenMs).Minutes
			};
			Console.Write($"[!]Found: {pokemon.Pokemon.PokemonType} ({foundPokemon.LifeExpectancy} minutes): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)} -");

			if (Data.ExcludedPokemon.Contains(foundPokemon.Type))
			{
				Console.WriteLine("excluded");
				return null;
			}

			if (foundPokemon.LifeExpectancy < c_minimumLifeExpectancy)
			{
				Console.WriteLine("expiring");
				return null;
			}

			if (alreadyFound.Contains(foundPokemon))
			{
				Console.WriteLine("duplicate");
				return null;
			}

			Console.WriteLine("tweeting");
			return foundPokemon;
		}

		//Select a random region that we didn't just search.
		private static Region SelectRegion(Region previous)
		{
			Random random = new Random();
			int regionIndex = -1;
			while (regionIndex < 0 || regionIndex == Data.Regions.IndexOf(previous))
				regionIndex = random.Next(Data.Regions.Count);

			return Data.Regions[regionIndex];
		}

		//Build a tweet with useful information about the pokemon, then cram in as many hashtags as will fit.
		private static string ComposeTweet(FoundPokemon pokemon, Region region)
		{
			string tweet = $"A wild {SpellCheckPokemon(pokemon.Type)} appeared! It will be near {region.Name} for {pokemon.LifeExpectancy} more minutes. https://www.google.com/maps/place/{pokemon.Location.Latitude},{pokemon.Location.Longitude}";
			if (Tweet.Length(tweet + " #" + SpellCheckPokemon(pokemon.Type, true)) < 140)
				tweet += " #" + SpellCheckPokemon(pokemon.Type, true);

			if (Tweet.Length(tweet + " #" + Regex.Replace(region.Name, @"\s+", "")) < 140)
				tweet += " #" + Regex.Replace(region.Name, @"\s+", "");

			if (Tweet.Length(tweet + " #Bellingham") < 140)
				tweet += " #Bellingham";

			if (Tweet.Length(tweet + " #PokemonGO") < 140)
				tweet += " #PokemonGO";

			return tweet;
		}


		//Generate user friendly and hashtag friendly pokemon names
		//Also, this client straight up spells some of the pokemon wrong.
		private static string SpellCheckPokemon(PokemonType pokemon, bool isHashtag = false)
		{
			switch (pokemon)
			{
				case PokemonType.Charmender:
					return "Charmander";
				case PokemonType.Clefary:
					return "Clefairy";
				case PokemonType.Farfetchd:
					return isHashtag ? "Farfetchd" : "Farfetch'd";
				case PokemonType.MrMime:
					return isHashtag ? "MrMime" : "Mr. Mime";
				case PokemonType.NidoranFemale:
					return isHashtag ? "Nidoran" : "Nidoran♀";
				case PokemonType.NidoranMale:
					return isHashtag ? "Nidoran" : "Nidoran♂";
				default:
					return pokemon.ToString();
			}
		}

		const int c_minimumLifeExpectancy = 5;
		private static string s_username = "";
		private static string s_password = "";
		private static string s_auth = "";
		private static PoClient s_poClient;

		private static string s_consumerToken = "CONSUMER_TOKEN";
		private static string s_consumerSecret = "CONSUMER_SECRET";
		private static string s_accessToken = "ACCESS_TOKEN";
		private static string s_accessSecret = "ACCESS_SECRET";
		private static IAuthenticatedUser s_twitterClient;
	}
}
