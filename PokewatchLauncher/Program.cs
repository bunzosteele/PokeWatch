using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PokewatchUtility;
using PokewatchUtility.DataTypes;

namespace PokewatchLauncher
{
	internal class Program
	{
		static void Main()
		{
			Console.Title = "Launcher";
			PokewatchLogger.Log("[!]Initializing PokewatchLauncher...", "Launcher");
			s_config = ConfigurationManager.ReadConfiguration("Launcher");
			Dictionary<PoGoAccount, List<Region>> workDistribution = DistributeWork(s_config.PoGoAccounts, s_config.Regions);
			foreach(PoGoAccount account in workDistribution.Keys)
			{
				int accountIndex = s_config.PoGoAccounts.IndexOf(account);
				List<int> regionIndeces = workDistribution[account].Select(r => s_config.Regions.IndexOf(r)).ToList();
				PokewatchLogger.Log("[!]Launching Pokewatch as " + AccountManager.GetAccountName(account) + " to monitor " + string.Join(", ", workDistribution[account].Select(r => r.Name)) + ".", "Launcher");
				PokewatchLogger.Log("[!]This includes " + workDistribution[account].SelectMany(r => r.Locations).Count() + " locations." , "Launcher");
				Launch(accountIndex, regionIndeces);
				Thread.Sleep(5000);
			}
			Thread.Sleep(Timeout.Infinite);
		}

		static void Launch(int accountIndex, List<int> regionIndeces)
		{
			string argumentString = accountIndex + " " + string.Join(" ", regionIndeces);
			Process process = new Process
			{
				StartInfo = { FileName = "Pokewatch.exe", Arguments = argumentString },
				EnableRaisingEvents = true
			};
			process.Exited += HandleCrash;

			process.Start();
		}

		static void HandleCrash(object o, EventArgs e)
		{
			Process process = (Process)o;
			if(process.ExitCode == 2)
			{
				//Something is misconfigured.
				PokewatchLogger.Log("[-]Something is likely misconfigured. Double check your Configuration.", "Launcher");
				PokewatchLogger.Log("[!]Exiting processs.", "Launcher");
			}else if (process.ExitCode == 3)
			{
				//PokemonGO connection issues
				PokewatchLogger.Log("[-]Error connecting to the PokemonGo servers. Servers may be down, your account/IP may be banned, or your credentials may be wrong.", "Launcher");
				PokewatchLogger.Log("[!]Restarting in 60 seconds...", "Launcher");
				Thread.Sleep(60000);
				PokewatchLogger.Log("[!]Attempting to recover from PokemonGo connection issues...", "Launcher");
				process.Start();
			}else if (process.ExitCode == 4)
			{
				//Twitter connection issues
				PokewatchLogger.Log("[-]Unable to authenticate Twitter account. Double check your Oauth credentials. You may have run into a Twitter rate limit, or you app may still need time to authenticate.", "Launcher");
				PokewatchLogger.Log("[!]Restarting in 15 minutes..", "Launcher");
				Thread.Sleep(900000);
				PokewatchLogger.Log("[!]Attempting to recover from Twitter authenitcation issues...", "Launcher");
				process.Start();
			} else if (process.ExitCode != 0)
			{
				//An unanticipated error occured during the run.
				TimeSpan runtime = DateTime.Now - process.StartTime;
				PokewatchLogger.Log("[-]Something went wrong after " + runtime.Seconds + "seconds.", "Launcher");
				PokewatchLogger.Log("[!]Restarting in 30 seconds...", "Launcher");
				Thread.Sleep(30000);
				PokewatchLogger.Log("[!]Restarting...", "Launcher");
				process.Start();
			}
		}

		static Dictionary<PoGoAccount, List<Region>> DistributeWork(List<PoGoAccount> accounts, IEnumerable<Region> regions)
		{
			Dictionary<PoGoAccount, List<Region>> distributedWork = new Dictionary<PoGoAccount, List<Region>>();
			List<Region> regionsToDistribute = regions.OrderByDescending(r => r.Locations.Count).ToList();
			while (regionsToDistribute.Count > 0)
			{
				if (accounts.Count > distributedWork.Keys.Count)
				{
					distributedWork.Add(accounts.First(a => !distributedWork.Keys.Contains(a)), new List<Region> { regionsToDistribute.First() });
					regionsToDistribute.RemoveAt(0);
				}
				else
				{
					PoGoAccount lightestLoad= distributedWork.Keys.OrderBy(a => distributedWork[a].SelectMany(r => r.Locations).Count()).First();
					distributedWork[lightestLoad].Add(regionsToDistribute.First());
					regionsToDistribute.RemoveAt(0);
				}
			}

			return distributedWork;
		}

		static Configuration s_config;
	}
}
