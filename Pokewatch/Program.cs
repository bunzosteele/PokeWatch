using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Google.Protobuf.Collections;
using PokewatchUtility;
using PokewatchUtility.DataTypes;
using POGOLib.Net;
using POGOProtos.Enums;
using POGOProtos.Map;
using POGOProtos.Map.Pokemon;
using Tweetinvi;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Models;
using Location = PokewatchUtility.DataTypes.Location;

namespace Pokewatch
{
	public class Program
	{
		public static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				PokewatchLogger.Log("[-]Missing Arguments. Indeces of account and regions must be specified.", "PokewatchUnknown");
				Environment.Exit(2);
			}
			s_config = ConfigurationManager.ReadConfiguration("Pokewatch");
			if (s_config == null)
				Environment.Exit(2);
			try
			{
				s_account = s_config.PoGoAccounts[int.Parse(args[0])];
				for (int i = 1; i < args.Length; i++)
				{
					s_regions.Add(s_config.Regions[int.Parse(args[i])]);
				}
			}
			catch
			{
				PokewatchLogger.Log("[-]Arguments do not align with provided configuration: " + string.Join(" ", args), "PokewatchUnknown");
				Environment.Exit(2);
			}

			try
			{
				s_scanAreas = s_regions.SelectMany(r => r.Locations.Select(l => new ScanArea
				{
					Location = l,
					Name = r.Name,
					Prefix = r.Prefix,
					Suffix = r.Suffix,
					Inclusions = r.Inclusions,
					Exclusions = r.Exclusions
				})).ToList();
				s_currentScan = s_scanAreas.First();
				s_scanIndex = 0;
			}
			catch
			{
				PokewatchLogger.Log("[-]Invalid Region Configuration", AccountManager.GetAccountName(s_account));
				Environment.Exit(2);
			}

			Console.Title = AccountManager.GetAccountName(s_account) + ": " + string.Join(", ", s_regions.Select(r => r.Name));

			s_pogoSession = AccountManager.SignIn(s_account, s_currentScan.Location);
			if (s_pogoSession == null)
			{
				PokewatchLogger.Log("[-]Unable to sign in to PokemonGo.", AccountManager.GetAccountName(s_account));
				Environment.Exit(3);
			}

			if (!PrepareTwitterClient())
				Environment.Exit(4);

			PokewatchLogger.Log("[+]Sucessfully signed in to twitter.", AccountManager.GetAccountName(s_account));

			s_pogoSession.Startup();

			s_pogoSession.AccessTokenUpdated += (sender, eventArgs) =>
			{
				PokewatchLogger.Log("[+]Access token updated.", AccountManager.GetAccountName(s_account));
			};

			s_pogoSession.Map.Update += (sender, eventArgs) =>
			{
				PokewatchLogger.Log("[+]Location Acknowleged. Searching...", AccountManager.GetAccountName(s_account));
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
				PokewatchLogger.Log("[!]All Regions Scanned. Starting over.", AccountManager.GetAccountName(s_account));
			}
			if(s_currentScan == null || s_currentScan.Name != s_scanAreas[s_scanIndex].Name)
				PokewatchLogger.Log("[!]Scanning new region: " + s_scanAreas[s_scanIndex].Name, AccountManager.GetAccountName(s_account));
			s_currentScan = s_scanAreas[s_scanIndex];
			SetLocation(s_currentScan.Location);
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

					if (Tweet.Length(tweet) > 140)
					{
						PokewatchLogger.Log("[-]Tweet exceeds 140 characters. Consider changing your template: " + tweet, AccountManager.GetAccountName(s_account));
						continue;
					}
					try
					{
						s_twitterClient.PublishTweet(tweet);
						PokewatchLogger.Log("[+]Tweet published: " + tweet, AccountManager.GetAccountName(s_account));
						s_lastTweet = DateTime.Now;
					}
					catch (Exception ex)
					{
						PokewatchLogger.Log("[-]Tweet failed to publish: " + tweet + " " + ex.Message, AccountManager.GetAccountName(s_account));
					}

					s_tweetedPokemon.Enqueue(foundPokemon);

					if (s_tweetedPokemon.Count > 10)
						s_tweetedPokemon.Dequeue();
				}
			}
			return true;
		}

		//Sign in to Twitter.
		private static bool PrepareTwitterClient()
		{
			if (s_config.TwitterConsumerToken.IsNullOrEmpty() || s_config.TwitterConsumerSecret.IsNullOrEmpty()
				|| s_config.TwitterAccessToken.IsNullOrEmpty() || s_config.TwitterConsumerSecret.IsNullOrEmpty())
			{
				PokewatchLogger.Log("[-]Must supply Twitter OAuth strings.", AccountManager.GetAccountName(s_account));
				return false;
			}

			PokewatchLogger.Log("[!]Signing in to Twitter.", AccountManager.GetAccountName(s_account));
			var userCredentials = Auth.CreateCredentials(s_config.TwitterConsumerToken, s_config.TwitterConsumerSecret, s_config.TwitterAccessToken, s_config.TwitterAccessSecret);
			ExceptionHandler.SwallowWebExceptions = false;
			try
			{
				s_twitterClient = User.GetAuthenticatedUser(userCredentials);
			}
			catch(Exception ex)
			{
				PokewatchLogger.Log("[-]Unable to authenticate Twitter account." + ex, AccountManager.GetAccountName(s_account));
				return false;
			}
			return true;
		}

		private static void SetLocation(Location location)
		{
			PokewatchLogger.Log($"[!]Setting location to {location.Latitude},{location.Longitude}", AccountManager.GetAccountName(s_account));
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

			if ((s_config.ExcludedPokemon.Contains(foundPokemon.Kind) && !(s_currentScan.Inclusions != null && s_currentScan.Inclusions.Contains(foundPokemon.Kind))) || (s_currentScan.Exclusions != null && s_currentScan.Exclusions.Contains(foundPokemon.Kind)))
			{
				PokewatchLogger.Log($"[!]Excluded: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}", AccountManager.GetAccountName(s_account));
				return null;
			}

			if (foundPokemon.LifeExpectancy < s_config.MinimumLifeExpectancy || foundPokemon.LifeExpectancy > 1000)
			{
				PokewatchLogger.Log($"[!]Expiring: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}", AccountManager.GetAccountName(s_account));
				return null;
			}

			if (alreadyFound.Contains(foundPokemon))
			{
				PokewatchLogger.Log($"[!]Duplicate: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}", AccountManager.GetAccountName(s_account));
				return null;
			}

			if ((lastTweet + TimeSpan.FromSeconds(s_config.RateLimit) > DateTime.Now) && !s_config.PriorityPokemon.Contains(foundPokemon.Kind))
			{
				PokewatchLogger.Log($"[!]Limiting: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}", AccountManager.GetAccountName(s_account));
				return null;
			}

			PokewatchLogger.Log($"[!]Tweeting: {foundPokemon.Kind} ({foundPokemon.LifeExpectancy} seconds): {Math.Round(foundPokemon.Location.Latitude, 6)},{Math.Round(foundPokemon.Location.Longitude, 6)}", AccountManager.GetAccountName(s_account));
			return foundPokemon;
		}

		//Build a tweet with useful information about the pokemon, then cram in as many hashtags as will fit.
		private static string ComposeTweet(FoundPokemon pokemon)
		{
			PokewatchLogger.Log("[!]Composing Tweet.", AccountManager.GetAccountName(s_account));
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
				PokewatchLogger.Log("[-]Failed to format tweet. If you made customizations, they probably broke it.", AccountManager.GetAccountName(s_account));
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

			PokewatchLogger.Log("[!]Sucessfully composed tweet.", AccountManager.GetAccountName(s_account));
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

		private static Configuration s_config;
		private static PoGoAccount s_account;
		private static List<Region> s_regions = new List<Region>();
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
