using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace ServiceBusTriggerWithCircuitBreaker
{
    public class ServiceBusQueueTriggerBindingProvider : ITriggerBindingProvider
    {
        private readonly ServiceBusQueueTriggerExtentionsConfigProvider _busQueueTriggerExtentionsConfigProvider;
        public ServiceBusQueueTriggerBindingProvider(ServiceBusQueueTriggerExtentionsConfigProvider busQueueTriggerExtentionsConfigProvider)
        {
            _busQueueTriggerExtentionsConfigProvider = busQueueTriggerExtentionsConfigProvider;
        }

        /// <summary>Try to bind using the specified context.</summary>
        /// <param name="context">The binding context.</param>
        /// <returns>A <see cref="T:Microsoft.Azure.WebJobs.Host.Triggers.ITriggerBinding" /> if successful, null otherwise.</returns>
        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            var parameter = context.Parameter;
            var attribute =
                parameter.GetCustomAttribute<ServiceBusQueueTriggerAttribute>(false);

            if (attribute == null)
                return Task.FromResult<ITriggerBinding>(null);

            if (!IsSupportBindingType(parameter.ParameterType))
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind SlackMessageTriggerAttribute to type '{0}'.", parameter.ParameterType));

            return
                Task.FromResult<ITriggerBinding>(new ServiceBusQueueTriggerBinding(parameter));
        }

        public bool IsSupportBindingType(Type t)
        {
            return t == typeof(Message) || t == typeof(string);
        }
    }
}
