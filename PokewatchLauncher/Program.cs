using System;
using System.Diagnostics;
using System.Threading;

namespace PokewatchLauncher
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Title = "Pokewatch";
			if (args.Length < 2)
			{
				Console.WriteLine("[!]Usage: PokewatchLauncher.exe -username -password [-auth]");
				return;
			}

			s_username = args[0];
			s_password = args[1];
			if (args.Length > 2)
				s_auth = args[2];
			Launch();
			Thread.Sleep(Timeout.Infinite);
		}

		static void Launch()
		{
			Process process = new Process();
			process.StartInfo.FileName = "Pokewatch.exe";
			process.StartInfo.Arguments = s_username + " " + s_password + " " + s_auth;
			process.EnableRaisingEvents = true;
			process.Exited += LaunchIfCrashed;
			Console.WriteLine("[!]Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
			process.Start();
		}


		//If the bot dies after start-up, it is likely due to a transient issue, take a break and try again later.
		static void LaunchIfCrashed(object o, EventArgs e)
		{
			Process process = (Process)o;
			if (process.ExitCode != 0)
			{
				Console.WriteLine("[-]Something went wrong. Waiting 2 minutes to restart:");
				Thread.Sleep(120000);
				Console.WriteLine("[!]Restarting.");
				Launch();
			}
			else
			{
				Environment.Exit(0);
			}
		}

		static string s_username = "";
		static string s_password = "";
		static string s_auth = "";
	}
}
