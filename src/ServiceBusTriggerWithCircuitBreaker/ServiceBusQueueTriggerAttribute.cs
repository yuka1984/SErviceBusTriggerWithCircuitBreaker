using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;

namespace ServiceBusTriggerWithCircuitBreaker
{
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public class ServiceBusQueueTriggerAttribute : Attribute
    {
        public ServiceBusQueueTriggerAttribute(string queueName)
        {
            QueueName = queueName;
        }

        /// <summary>
        /// Gets or sets the app setting name that contains the Service Bus connection string.
        /// </summary>
        public string Connection { get; set; }

        public string QueueName { get; }

        public int BreakErrorSpan { get; set; } = 60000;
        public int BreakErrorCount { get; set; } = 5;
        public int MinOpenTime { get; set; } = 60000;
        public int MaxOpenTime { get; set; } = 300000;
    }
}
