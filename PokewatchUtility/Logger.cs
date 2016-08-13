using System;
using System.IO;

namespace PokewatchUtility
{
	public sealed class PokewatchLogger
	{
		public static void Log(string message, string signature)
		{
			Console.WriteLine(message);
			Directory.CreateDirectory("logs");
			using (StreamWriter w = File.AppendText(Path.Combine("logs", signature + "-log.txt")))
			{
				w.WriteLine(DateTime.Now + ": " + message);
			}
		}
	}
}
