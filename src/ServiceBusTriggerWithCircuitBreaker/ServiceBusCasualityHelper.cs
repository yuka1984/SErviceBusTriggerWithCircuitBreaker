using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace ServiceBusTriggerWithCircuitBreaker
{
    internal static class ServiceBusCausalityHelper
    {
        private const string ParentGuidFieldName = "$AzureWebJobsParentId";

        public static void EncodePayload(Guid functionOwner, Message msg)
        {
            msg.UserProperties[ParentGuidFieldName] = functionOwner.ToString();
        }

        public static Guid? GetOwner(Message msg)
        {
            object parent;
            if (msg.UserProperties.TryGetValue(ParentGuidFieldName, out parent))
            {
                var parentString = parent as string;
                if (parentString != null)
                {
                    Guid parentGuid;
                    if (Guid.TryParse(parentString, out parentGuid))
                    {
                        return parentGuid;
                    }
                }
            }
            return null;
        }
    }
}
