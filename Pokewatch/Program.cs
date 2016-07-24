using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Google.Protobuf.Collections;
using Pokewatch.Datatypes;
using Pokewatch.DataTypes;
using POGOLib.Net;
using POGOLib.Pokemon;
using POGOProtos.Enums;
using POGOProtos.Map;
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
			try
			{
				string json = File.ReadAllText("Configuration.json");
				s_config = new JavaScriptSerializer().Deserialize<Configuration>(json);
			}
			catch(Exception ex)
			{
				Log("[-]Unable to load config.");
				Log(ex.Message);
				return;
			}

			if ((s_config.PTCUsername.IsNullOrEmpty() || s_config.PTCPassword.IsNullOrEmpty()) && (s_config.GAPassword.IsNullOrEmpty() || s_config.GAUsername.IsNullOrEmpty()))
			{
				Log("[-]Username and password must be supplied for either PTC or Google.");
				return;
			}

			if (!PrepareTwitterClient())
				return;

			Log("[+]Sucessfully signed in to twitter.");

			Log("[!]Attempting to sign in to PokemonGo using PTC.");
			if (PrepareClient(s_config.PTCUsername, s_config.PTCPassword, LoginProvider.PokemonTrainerClub))
			{
				Log("[+]Sucessfully logged in to PokemonGo.");
			}
			else
			{
				Log("[-]Unable to log in using PTC.");
				Log("[!]Attempting to sign in to PokemonGo using Google.");
				if (PrepareClient(s_config.GAUsername, s_config.GAPassword, LoginProvider.GoogleAuth))
				{
					Log("[+]Sucessfully logged in to PokemonGo.");
				}
				else
				{
					Log("[-]Unable to log in using Google.");
					throw new Exception();
				}
			}

			if (!Search())
				throw new Exception();
		}

		private static bool Search()
		{
			if (s_config.Regions.Count == 0)
			{
				Log("[-]No Regions to search.");
				return true;
			}
			Queue<FoundPokemon> tweetedPokemon = new Queue<FoundPokemon>();
			int regionIndex = -1;
			DateTime lastTweet = DateTime.MinValue;
			while (true)
			{
				regionIndex++;
				if (regionIndex == s_config.Regions.Count)
					regionIndex = 0;

				Region region = s_config.Regions[regionIndex];
				Log($"[!]Searching Region: {region.Name}");
				foreach (Location location in region.Locations)
				{
					SetLocation(location);

					//Wait so we don't clobber api and to let the heartbeat catch up to our new location. (Minimum heartbeat time is 4000ms)
					Thread.Sleep(5000);
					Log("[!]Searching nearby cells.");
					RepeatedField<MapCell> mapCells;
					try
					{
						var mapObjects = s_poClient.MapObjects;
						mapCells = mapObjects.MapCells;
					}
					catch
					{
						Log("[-]Heartbeat has failed. Terminating Connection.");
						return false;
					}
					foreach (var mapCell in mapCells)
					{
						foreach (WildPokemon pokemon in mapCell.WildPokemons)
						{
							FoundPokemon foundPokemon = ProcessPokemon(pokemon, tweetedPokemon, lastTweet);

							if (foundPokemon == null)
								continue;

							string tweet = ComposeTweet(foundPokemon, region);
							s_twitterClient.PublishTweet(tweet);
							Log("[+]Tweet published: " + tweet);
							lastTweet = DateTime.Now;

							tweetedPokemon.Enqueue(foundPokemon);
							if (tweetedPokemon.Count > 10)
								tweetedPokemon.Dequeue();
						}
					}
				}
				Log("[!]Finished Searching " + region.Name);
			}
		}

		//Sign in to PokemonGO
		private static bool PrepareClient(string username, string password, LoginProvider loginProvider)
		{
			s_poClient = new PoClient(username, loginProvider);

			// Client requires a location be set when signing in.
			SetLocation(s_config.Regions.First().Locations.First());
			return s_poClient.Authenticate(password);
		}

		//Sign in to Twitter.
		private static bool PrepareTwitterClient()
		{
			if (s_config.TwitterConsumerToken.IsNullOrEmpty() || s_config.TwitterConsumerSecret.IsNullOrEmpty()
				|| s_config.TwitterAccessToken.IsNullOrEmpty() || s_config.TwitterConsumerSecret.IsNullOrEmpty())
			{
				Log("[-]Must supply Twitter OAuth strings.");
				return false;
			}

			Log("[!]Signing in to Twitter.");
			var userCredentials = Auth.CreateCredentials(s_config.TwitterConsumerToken, s_config.TwitterConsumerSecret, s_config.TwitterAccessToken, s_config.TwitterAccessSecret);
			ExceptionHandler.SwallowWebExceptions = false;
			try
			{
				s_twitterClient = User.GetAuthenticatedUser(userCredentials);
			}
			catch
			{
				Log("[-]Unable to authenticate Twitter account.");
				return false;
			}
			return true;
		}

		private static void SetLocation(Location location)
		{
			Log($"[!]Setting location to {location.Latitude},{location.Longitude}");
			s_poClient.SetGpsData(location.Latitude, location.Longitude);
		}

		//Evaluate if a pokemon is worth tweeting about.
		private static FoundPokemon ProcessPokemon(WildPokemon pokemon, Queue<FoundPokemon> alreadyFound, DateTime lastTweet)
		{
			FoundPokemon foundPokemon = new FoundPokemon
			{
				Location = new Location { Latitude = pokemon.Latitude, Longitude = pokemon.Longitude},
				Type = pokemon.Pokemon.PokemonType,
				LifeExpectancy = pokemon.TimeTillHiddenMs / 1000
			};

			if (s_config.ExcludedPokemon.Contains(foundPokemon.Type))
			{
				Log($"[!]Excluded: {foundPokemon.Type} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if (foundPokemon.LifeExpectancy < s_config.MinimumLifeExpectancy)
			{
				Log($"[!]Expiring: {foundPokemon.Type} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if (alreadyFound.Contains(foundPokemon))
			{
				Log($"[!]Duplicate: {foundPokemon.Type} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if ((lastTweet + TimeSpan.FromSeconds(s_config.RateLimit) > DateTime.Now) && !s_config.PriorityPokemon.Contains(foundPokemon.Type))
			{
				Log($"[!]Limiting: {foundPokemon.Type} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			Log($"[!]Tweeting: {foundPokemon.Type} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
			return foundPokemon;
		}

		//Build a tweet with useful information about the pokemon, then cram in as many hashtags as will fit.
		private static string ComposeTweet(FoundPokemon pokemon, Region region)
		{
			Log("[!]Composing Tweet");
			string latitude = pokemon.Location.Latitude.ToString(System.Globalization.CultureInfo.GetCultureInfo("en-us"));
			string longitude = pokemon.Location.Longitude.ToString(System.Globalization.CultureInfo.GetCultureInfo("en-us"));
			string mapsLink = $"https://www.google.com/maps/place/{latitude},{longitude}";
			string expiration = DateTime.Now.AddSeconds(pokemon.LifeExpectancy).ToLocalTime().ToShortTimeString();
			string tweet = "";

			if (s_config.PriorityPokemon.Contains(pokemon.Type))
			{
				tweet = $"BREAKING NEWS: {SpellCheckPokemon(pokemon.Type)} has appeared {region.Prefix} {region.Name} {region.Suffix}! Hurry, it will vanish at {expiration}! {mapsLink}";
			}
			else
			{
				tweet = $"A wild {SpellCheckPokemon(pokemon.Type)} appeared! It will be {region.Prefix} {region.Name} {region.Suffix} until {expiration}. {mapsLink}";
			}

			tweet = Regex.Replace(tweet, @"\s\s", @" ");
			tweet = Regex.Replace(tweet, @"\s[!]", @"!");

			if (Tweet.Length(tweet + " #" + SpellCheckPokemon(pokemon.Type, true)) < 140)
				tweet += " #" + SpellCheckPokemon(pokemon.Type, true);

			if (Tweet.Length(tweet + " #" + Regex.Replace(region.Name, @"\s+", "")) < 140)
				tweet += " #" + Regex.Replace(region.Name, @"\s+", "");

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
				case PokemonType.Geoduge:
					return "Geodude";
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

		private static void Log(string message)
		{
			using (StreamWriter w = File.AppendText("log.txt"))
			{
				w.WriteLine(message);
			}
		}

		private static Configuration s_config;
		private static IAuthenticatedUser s_twitterClient;
		private static PoClient s_poClient;
	}
}
