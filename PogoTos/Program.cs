using System.Linq;
using System.Threading;
using Google.Protobuf;
using PokewatchUtility;
using PokewatchUtility.DataTypes;
using POGOLib.Net;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;

namespace PogoTos
{
	class Program
	{
		static void Main(string[] args)
		{
			Configuration config = ConfigurationManager.ReadConfiguration();
			if (config == null)
				return;

			Location defaultLocation;
			try
			{
				defaultLocation = config.Regions.SelectMany(r => r.Locations.Select(l => new ScanArea
				{
					Location = l,
					Name = r.Name,
					Prefix = r.Prefix,
					Suffix = r.Suffix
				})).First().Location;
			}
			catch
			{
				PokewatchLogger.Log("[-]Invalid Region Configuration");
				return;
			}

			foreach (PoGoAccount account in config.PoGoAccounts)
			{
				Thread.Sleep(10000);
				Session session = AccountManager.SignIn(account, defaultLocation);
				Thread.Sleep(10000);
				session.Startup();
				PokewatchLogger.Log("[!]Attempting to Accept ToS.");
				Thread.Sleep(10000);
				var acceptTosRaw = session.RpcClient.SendRemoteProcedureCall(new Request
				{
					RequestType = RequestType.MarkTutorialComplete,
					RequestMessage = new MarkTutorialCompleteMessage
					{
						SendMarketingEmails = false,
						SendPushNotifications = false,
						TutorialsCompleted = { 0 }
					}.ToByteString()
				});

				var acceptTos = MarkTutorialCompleteResponse.Parser.ParseFrom(acceptTosRaw);
				if (acceptTos.Success)
				{
					PokewatchLogger.Log("[+]ToS accepted.");
				}
				else
				{
					PokewatchLogger.Log("[!]Unable to accept Tos.");
				}
			}
		}
	}
}
