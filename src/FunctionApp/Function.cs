using System;
using System.IO;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using ServiceBusTriggerWithCircuitBreaker;

namespace FunctionApp
{
    public static class Function
    {
        [FunctionName("Function")]
        public static void Run([ServiceBusQueueTrigger("test", Connection = "ServiceBusConnection")]Message myQueueItem, TraceWriter log)
        {
            var message = Encoding.UTF8.GetString(myQueueItem.Body);
            log.Info($"C# ServiceBus queue trigger function processed message: {message}");
            throw new Exception();
        }
    }
}
