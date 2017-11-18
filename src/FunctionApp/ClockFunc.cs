using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace FunctionApp
{
    public static class ClockFunc
    {
        private static int i = 1;

        [FunctionName("ClockFunc")]
        public static void Run(
            [TimerTrigger("*/1 * * * * *")]TimerInfo myTimer
            , [ServiceBus("test", Connection = "ServiceBusConnection")] out string message
            , TraceWriter log)
        {
            message = (i++).ToString();
        }
    }
}
