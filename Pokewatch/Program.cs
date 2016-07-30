using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Google.Protobuf.Collections;
using Pokewatch.Datatypes;
using Pokewatch.DataTypes;
using POGOLib.Net;
using POGOLib.Net.Authentication;
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

			try
			{
				s_scanAreas = s_config.Regions.SelectMany(r => r.Locations.Select(l => new ScanArea
				{
					Location = l,
					Name = r.Name,
					Prefix = r.Prefix,
					Suffix = r.Suffix
				})).ToList();
				s_currentScan = s_scanAreas.First();
				s_scanIndex = 0;
			}
			catch
			{
				Log("[-]Invalid Region Configuration");
				return;
			}

			if ((s_config.PTCUsername.IsNullOrEmpty() || s_config.PTCPassword.IsNullOrEmpty()) && (s_config.GAPassword.IsNullOrEmpty() || s_config.GAUsername.IsNullOrEmpty()))
			{
				Log("[-]Username and password must be supplied for either PTC or Google.");
				return;
			}

			if (!PrepareTwitterClient())
				throw new Exception();

			Log("[+]Sucessfully signed in to twitter.");
			if (!PrepareClient())
			{
				Log("[-]Unable to sign in to PokemonGo.");
				throw new Exception();
			}

			s_pogoSession.AccessTokenUpdated += (sender, eventArgs) =>
			{
				Log("[+]Access token updated.");
			};

			s_pogoSession.Map.Update += (sender, eventArgs) =>
			{
				Log("[+]Location Acknowleged. Searching...");
				if (Search())
					UpdateLocation();
			};

			Console.CancelKeyPress += (sender, eArgs) => {
				QuitEvent.Set();
				eArgs.Cancel = true;
			};

			QuitEvent.WaitOne();
		}

		private static void UpdateLocation()
		{
			s_scanIndex++;
			if (s_scanIndex == s_scanAreas.Count)
			{
				s_scanIndex = 0;
				Log("[!]All Regions Scanned. Restarting.");
			}
			s_currentScan = s_scanAreas[s_scanIndex];
			SetLocation(s_currentScan.Location);
			Log($"[!]Scanning: {s_currentScan.Name} ({s_currentScan.Location.Latitude}, {s_currentScan.Location.Longitude})");
		}

		private static bool Search()
		{
			RepeatedField<MapCell> mapCells = s_pogoSession.Map.Cells;
			foreach (var mapCell in mapCells)
			{
				foreach (WildPokemon pokemon in mapCell.WildPokemons)
				{
					FoundPokemon foundPokemon = ProcessPokemon(pokemon, s_tweetedPokemon, s_lastTweet);

					if (foundPokemon == null)
						continue;

					string tweet = ComposeTweet(foundPokemon);

					if (tweet == null)
						throw new Exception();

					if (tweet.Length > 140)
					{
						Log("[-]Tweet exceeds 140 characters. Consider changing your template.");
						continue;
					}
					try
					{
						s_twitterClient.PublishTweet(tweet);
						Log("[+]Tweet published: " + tweet);
						s_lastTweet = DateTime.Now;
					}
					catch (Exception ex)
					{
						Log("[-]Tweet failed to publish: " + tweet + " " + ex.Message);
					}

					s_tweetedPokemon.Enqueue(foundPokemon);

					if (s_tweetedPokemon.Count > 10)
						s_tweetedPokemon.Dequeue();
				}
			}
			Log($"[!]Scanning: {s_currentScan.Name} ({s_currentScan.Location.Latitude}, {s_currentScan.Location.Longitude})");
			return true;
		}

		//Sign in to PokemonGO
		private static bool PrepareClient()
		{
			if (!s_config.PTCUsername.IsNullOrEmpty() && !s_config.PTCPassword.IsNullOrEmpty())
			{
				try
				{
					Log("[!]Attempting to sign in to PokemonGo using PTC.");
					s_pogoSession = Login.GetSession(s_config.PTCUsername, s_config.PTCPassword, LoginProvider.PokemonTrainerClub, s_currentScan.Location.Latitude, s_currentScan.Location.Longitude);
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
					s_pogoSession = Login.GetSession(s_config.GAUsername, s_config.GAPassword, LoginProvider.GoogleAuth, s_currentScan.Location.Latitude, s_currentScan.Location.Longitude);
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
				Kind = pokemon.PokemonData.PokemonId,
				LifeExpectancy = pokemon.TimeTillHiddenMs / 1000
			};

			if (s_config.ExcludedPokemon.Contains(foundPokemon.Kind))
			{
				Log($"[!]Excluded: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if (foundPokemon.LifeExpectancy < s_config.MinimumLifeExpectancy)
			{
				Log($"[!]Expiring: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if (alreadyFound.Contains(foundPokemon))
			{
				Log($"[!]Duplicate: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			if ((lastTweet + TimeSpan.FromSeconds(s_config.RateLimit) > DateTime.Now) && !s_config.PriorityPokemon.Contains(foundPokemon.Kind))
			{
				Log($"[!]Limiting: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
				return null;
			}

			Log($"[!]Tweeting: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}");
			return foundPokemon;
		}

		//Build a tweet with useful information about the pokemon, then cram in as many hashtags as will fit.
		private static string ComposeTweet(FoundPokemon pokemon)
		{
			Log("[!]Composing Tweet");
			string latitude = pokemon.Location.Latitude.ToString(System.Globalization.CultureInfo.GetCultureInfo("en-us"));
			string longitude = pokemon.Location.Longitude.ToString(System.Globalization.CultureInfo.GetCultureInfo("en-us"));
			string mapsLink = s_config.Pokevision ? $"https://pokevision.com/#/@{latitude},{longitude}" : $"https://www.google.com/maps/place/{latitude},{longitude}";
			string expiration = DateTime.Now.AddSeconds(pokemon.LifeExpectancy).ToLocalTime().ToShortTimeString();
			string tweet = "";

			try
			{
				tweet = string.Format(s_config.PriorityPokemon.Contains(pokemon.Kind) ? s_config.PriorityTweet : s_config.RegularTweet,
					SpellCheckPokemon(pokemon.Kind),
					s_currentScan.Prefix,
					s_currentScan.Name,
					s_currentScan.Suffix,
					expiration,
					mapsLink);
			}
			catch
			{
				Log("[-]Failed to format tweet. If you made customizations, they probably broke it.");
				return null;
			}

			tweet = Regex.Replace(tweet, @"\s\s", @" ");
			tweet = Regex.Replace(tweet, @"\s[!]", @"!");

			Regex hashtag = new Regex("[^a-zA-Z0-9]");

			if (s_config.TagPokemon && (Tweet.Length(tweet + " #" + hashtag.Replace(SpellCheckPokemon(pokemon.Kind), "")) < 140))
				tweet += " #" + hashtag.Replace(SpellCheckPokemon(pokemon.Kind), "");

			if (s_config.TagRegion && (Tweet.Length(tweet + " #" + hashtag.Replace(s_currentScan.Name, "")) < 140))
				tweet += " #" + hashtag.Replace(s_currentScan.Name, "");

			foreach(string tag in s_config.CustomTags)
			{
				if(Tweet.Length(tweet + " #" + hashtag.Replace(tag, "")) < 140)
					tweet += " #" + hashtag.Replace(tag, "");
			}

			byte[] bytes = Encoding.Default.GetBytes(tweet);
			tweet = Encoding.UTF8.GetString(bytes);
			Log("[!]Sucessfully composed tweet.");
			return tweet;
		}

		//Generate user friendly pokemon names
		private static string SpellCheckPokemon(PokemonId pokemon)
		{
			string display;
			switch (pokemon)
			{
				case PokemonId.Farfetchd:
					display = "Farfetch'd";
					break;
				case PokemonId.MrMime:
					display = "Mr. Mime";
					break;
				case PokemonId.NidoranFemale:
					display = "Nidoran♀";
					break;
				case PokemonId.NidoranMale:
					display = "Nidoran♂";
					break;
				default:
					display = pokemon.ToString();
					break;
			}
			if (s_config.PokemonOverrides != null && s_config.PokemonOverrides.Any(po => po.Kind == pokemon))
			{
				display = s_config.PokemonOverrides.First(po => po.Kind == pokemon).Display;
			}
			return display;
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
		private static DateTime s_lastTweet = DateTime.MinValue;
		private static Queue<FoundPokemon> s_tweetedPokemon = new Queue<FoundPokemon>();
		private static List<ScanArea> s_scanAreas = new List<ScanArea>();
		private static ScanArea s_currentScan;
		private static int s_scanIndex;
		private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);
	}
}
