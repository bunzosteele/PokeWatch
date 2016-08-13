using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PokewatchUtility.DataTypes;
using POGOLib.Net;
using POGOLib.Net.Authentication;
using POGOLib.Pokemon.Data;
using Tweetinvi.Core.Extensions;

namespace PokewatchUtility
{
	public class AccountManager
	{
		public static Session SignIn(PoGoAccount account, Location defaultLocation)
		{
			if ((account.PTCUsername.IsNullOrEmpty() || account.PTCPassword.IsNullOrEmpty()) && (account.GAPassword.IsNullOrEmpty() || account.GAUsername.IsNullOrEmpty()))
			{
				PokewatchLogger.Log("[-]Username and password must be supplied for either PTC or Google.", GetAccountName(account));
				return null;
			}

			if (!account.PTCUsername.IsNullOrEmpty() && !account.PTCPassword.IsNullOrEmpty())
			{
				try
				{
					PokewatchLogger.Log("[!]Attempting to sign in to PokemonGo as " + account.PTCUsername + " using PTC.", GetAccountName(account));
					var pogoSession = Login.GetSession(account.PTCUsername, account.PTCPassword, LoginProvider.PokemonTrainerClub, defaultLocation.Latitude, defaultLocation.Longitude);
					PokewatchLogger.Log("[+]Sucessfully logged in to PokemonGo using PTC.", GetAccountName(account));
					return pogoSession;
				}
				catch
				{
					PokewatchLogger.Log("[-]Unable to log in using PTC.", GetAccountName(account));
				}
			}
			if (!account.GAUsername.IsNullOrEmpty() && !account.GAPassword.IsNullOrEmpty())
			{
				try
				{
					PokewatchLogger.Log("[!]Attempting to sign in to PokemonGo as " + account.GAUsername + " using Google.", GetAccountName(account));
					var pogoSession = Login.GetSession(account.GAUsername, account.GAPassword, LoginProvider.GoogleAuth, defaultLocation.Latitude, defaultLocation.Longitude);
					PokewatchLogger.Log("[+]Sucessfully logged in to PokemonGo using Google.", GetAccountName(account));
					return pogoSession;
				}
				catch
				{
					PokewatchLogger.Log("[-]Unable to log in using Google.", GetAccountName(account));
				}
			}
			return null;
		}

		public static string GetAccountName(PoGoAccount account)
		{
			return (!account.PTCUsername.IsNullOrEmpty() ? account.PTCUsername : account.GAUsername).Split('@')[0];
		}
	}
}
