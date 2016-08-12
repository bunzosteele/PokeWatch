using System;
using System.IO;

namespace PokewatchUtility
{
	public sealed class PokewatchLogger
	{
		public static void Log(string message, string fileName = "log.txt")
		{
			Console.WriteLine(message);
			using (StreamWriter w = File.AppendText(fileName))
			{
				w.WriteLine(DateTime.Now + ": " + message);
			}
		}
	}
}
