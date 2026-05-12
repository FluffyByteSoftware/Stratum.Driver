using System.Threading.Tasks;
using Stratum.SystemTools.Clock;
using Stratum.SystemTools.Logger;
using Stratum.SystemTools.Storage;

namespace Stratum.Driver;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            DiskManager.Initialize("./data");
            Heartbeat.Initialize(tickRateHz: 30);

            if(args.Length == 0)
            {
                var message = new ScribeMessage(ScribeSeverity.Debug, "No arguments provided.");
                Scribe.Pump(message);
            }
        }
        finally
        {
            // Must shutdown the heartbeat first.
            await Heartbeat.Instance.StopAsync();

            await Scribe.ShutdownAsync();
            
            // Must shutdown the disk manager last to ensure all logs are flushed to disk.
            await DiskManager.Instance.ShutdownAsync();

        }
    }
}