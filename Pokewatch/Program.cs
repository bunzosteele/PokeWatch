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
using POGOLib.Net.Authentication;
using POGOLib.Pokemon;
using POGOLib.Pokemon.Data;
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
			if (PrepareClient())
			{
				Log("[+]Sucessfully signed in to PokemonGo, beginning search.");
			};

			if (!Search())
				throw new Exception();
		}

		private static bool Search()
		{
			Queue<FoundPokemon> tweetedPokemon = new Queue<FoundPokemon>();
			DateTime lastTweet = DateTime.MinValue;
			Random random = new Random();
			while (true)
			{
				int regionIndex = random.Next(s_config.Regions.Count);
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
						mapCells = s_pogoSession.Map.Cells;
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

							try
							{
								s_twitterClient.PublishTweet(tweet);
							}
							catch(Exception ex)
							{
								Log("[-]Tweet failed to publish: " + tweet + " " + ex.Message);
								continue;
							}

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
		private static bool PrepareClient()
		{
			Location defaultLocation;
			try
			{
				defaultLocation = s_config.Regions.First().Locations.First();
			}
			catch
			{
				Log("[-]No locations have been supplied.");
				return false;
			}
			if (!s_config.PTCUsername.IsNullOrEmpty() && !s_config.PTCPassword.IsNullOrEmpty())
			{
				try
				{
					Log("[!]Attempting to sign in to PokemonGo using PTC.");
					s_pogoSession = Login.GetSession(s_config.PTCUsername, s_config.PTCPassword, LoginProvider.PokemonTrainerClub, defaultLocation.Latitude, defaultLocation.Longitude);
					Log("[+]Sucessfully logged in to PokemonGo using PTC.");
					return true;
				}
				catch
				{
					Log("[-]Unable to log in using PTC.");
				}
			}
			if (!s_config.GAUsername.IsNullOrEmpty() && !s_config.GAPassword.IsNullOrEmpty())
			{
				try
				{
					Log("[!]Attempting to sign in to PokemonGo using Google.");
					s_pogoSession = Login.GetSession(s_config.GAUsername, s_config.GAPassword, LoginProvider.GoogleAuth, defaultLocation.Latitude, defaultLocation.Longitude);
					Log("[+]Sucessfully logged in to PokemonGo using Google.");
					return true;
				}
				catch
				{
					Log("[-]Unable to log in using Google.");
				}
			}
			return false;
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
				Log("[-]Unable to authenticate Twitter account. Check your internet connection, verify your OAuth credential strings. If your bot is new, Twitter may still be validating your application.");
				return false;
			}
			return true;
		}

		private static void SetLocation(Location location)
		{
			Log($"[!]Setting location to {location.Latitude},{location.Longitude}");
			s_pogoSession.Player.SetCoordinates(location.Latitude, location.Longitude);
		}

		//Evaluate if a pokemon is worth tweeting about.
		private static FoundPokemon ProcessPokemon(WildPokemon pokemon, Queue<FoundPokemon> alreadyFound, DateTime lastTweet)
		{
			FoundPokemon foundPokemon = new FoundPokemon
			{
				Location = new Location { Latitude = pokemon.Latitude, Longitude = pokemon.Longitude},
				Type = pokemon.PokemonData.PokemonId,
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
		private static string SpellCheckPokemon(PokemonId pokemon, bool isHashtag = false)
		{
			switch (pokemon)
			{
				case PokemonId.Farfetchd:
					return isHashtag ? "Farfetchd" : "Farfetch'd";
				case PokemonId.MrMime:
					return isHashtag ? "MrMime" : "Mr. Mime";
				case PokemonId.NidoranFemale:
					return isHashtag ? "Nidoran" : "Nidoran♀";
				case PokemonId.NidoranMale:
					return isHashtag ? "Nidoran" : "Nidoran♂";
				default:
					return pokemon.ToString();
			}
		}

		private static void Log(string message)
		{
			Console.WriteLine(message);
			using (StreamWriter w = File.AppendText("log.txt"))
			{
				w.WriteLine(DateTime.Now + ": " + message);
			}
		}

		private static Configuration s_config;
		private static IAuthenticatedUser s_twitterClient;
		private static Session s_pogoSession;
	}
}
