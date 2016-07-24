using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PokewatchLauncher
{
	internal class Program
	{
		static void Main()
		{
			Console.Title = "Pokewatch";
			Launch();
			Thread.Sleep(Timeout.Infinite);
		}

		static void Launch()
		{
			Process process = new Process
			{
				StartInfo = { FileName = "Pokewatch.exe" },
				EnableRaisingEvents = true
			};
			process.Exited += LaunchIfCrashed;
			Log("[!]Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
			process.Start();
		}

		//If the bot dies after start-up, it is likely due to a transient issue, take a break and try again later.
		static void LaunchIfCrashed(object o, EventArgs e)
		{
			Process process = (Process)o;
			if (process.ExitCode != 0)
			{
				Log($"[-]Something went wrong at {DateTime.Now}. Waiting 2 minutes to restart:");
				Thread.Sleep(120000);
				Log("[!]Restarting");
				Launch();
			}
			else
			{
				Log("[!]Exiting");
				Environment.Exit(0);
			}
		}

		private static void Log(string message)
		{
			using (StreamWriter w = File.AppendText("log.txt"))
			{
				w.WriteLine(message);
			}
		}
	}
}
