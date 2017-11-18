using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json;

namespace ServiceBusTriggerWithCircuitBreaker
{
    public class ServiceBusQueueTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        public ServiceBusQueueTriggerBinding(ParameterInfo parameter)
        {
            _parameter = parameter;
        }

        /// <summary>
        /// Perform a bind to the specified value using the specified binding context.
        /// </summary>
        /// <param name="value">The value to bind to.</param>
        /// <param name="context">The binding context.</param>
        /// <returns>A task that returns the <see cref="T:Microsoft.Azure.WebJobs.Host.Triggers.ITriggerData" /> for the binding.</returns>
        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            if (value is Message)
            {
                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    {"data", value}
                };

                object argument;
                if (_parameter.ParameterType == typeof(string))
                    argument = JsonConvert.SerializeObject(value, Formatting.Indented);
                else
                    argument = value;

                IValueBinder valueBinder = new ServiceBusValueBinder(_parameter, argument);
                return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, bindingData));
            }
            throw new Exception();
        }

        /// <summary>
        /// Creates a <see cref="T:Microsoft.Azure.WebJobs.Host.Listeners.IListener" /> for the trigger parameter.
        /// </summary>
        /// <param name="context">The <see cref="T:Microsoft.Azure.WebJobs.Host.Listeners.ListenerFactoryContext" /> to use.</param>
        /// <returns>The <see cref="T:Microsoft.Azure.WebJobs.Host.Listeners.IListener" />.</returns>
        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            var attribute = GetResolvedAttribute<ServiceBusQueueTriggerAttribute>(_parameter);
            var connectionSring = AmbientConnectionStringProvider.Instance.GetConnectionString(attribute.Connection);
            return
                Task.FromResult<IListener>(new ServiceBusQueueListener(
                    connectionSring
                    , attribute.QueueName
                    , new ServiceBusTriggerExecutor(context.Executor)));
        }

        /// <summary>Get a description of the binding.</summary>
        /// <returns>The <see cref="T:Microsoft.Azure.WebJobs.Host.Protocols.ParameterDescriptor" /></returns>
        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ServiceBusQueueTriggerParameterDescriptor()
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "ServiceBusQueueTrigger",
                    Description = "ServiceBusQueue trigger fired",
                    DefaultValue = "Sample"
                }
            };
        }

        /// <summary>The trigger value type that this binding binds to.</summary>
        public Type TriggerValueType => typeof(Message);

        /// <summary>Gets the binding data contract.</summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract { get; }

        private static TAttribute GetResolvedAttribute<TAttribute>(ParameterInfo parameter) where TAttribute : Attribute
        {
            var attribute = parameter.GetCustomAttribute<TAttribute>(true);

            var attributeConnectionProvider = attribute as IConnectionProvider;
            if (attributeConnectionProvider != null && string.IsNullOrEmpty(attributeConnectionProvider.Connection))
            {
                var connectionProviderAttribute =
                    attribute.GetType().GetCustomAttribute<ConnectionProviderAttribute>();
                if (connectionProviderAttribute?.ProviderType != null)
                {
                    var connectionOverrideProvider =
                        GetHierarchicalAttributeOrNull(parameter, connectionProviderAttribute.ProviderType) as
                            IConnectionProvider;
                    if (connectionOverrideProvider != null &&
                        !string.IsNullOrEmpty(connectionOverrideProvider.Connection))
                        attributeConnectionProvider.Connection = connectionOverrideProvider.Connection;
                }
            }

            return attribute;
        }

        private static T GetHierarchicalAttributeOrNull<T>(ParameterInfo parameter) where T : Attribute
        {
            return (T)GetHierarchicalAttributeOrNull(parameter, typeof(T));
        }

        private static Attribute GetHierarchicalAttributeOrNull(ParameterInfo parameter, Type attributeType)
        {
            if (parameter == null)
                return null;

            var attribute = parameter.GetCustomAttribute(attributeType);
            if (attribute != null)
                return attribute;

            var method = parameter.Member as MethodInfo;
            if (method == null)
                return null;
            return GetHierarchicalAttributeOrNull(method, attributeType);
        }

        private static T GetHierarchicalAttributeOrNull<T>(MethodInfo method) where T : Attribute
        {
            return (T)GetHierarchicalAttributeOrNull(method, typeof(T));
        }

        private static Attribute GetHierarchicalAttributeOrNull(MethodInfo method, Type type)
        {
            var attribute = method.GetCustomAttribute(type);
            if (attribute != null)
                return attribute;

            attribute = method.DeclaringType.GetCustomAttribute(type);
            if (attribute != null)
                return attribute;

            return null;
        }

        private class ServiceBusValueBinder : ValueBinder, IDisposable
        {
            private readonly Message _value;
            private List<IDisposable> _disposables;

            public ServiceBusValueBinder(ParameterInfo parameter, object value,
                List<IDisposable> disposables = null)
                : base(parameter.ParameterType)
            {
                _value = (Message)value;
                _disposables = disposables;
            }

            public void Dispose()
            {
                if (_disposables != null)
                {
                    foreach (var d in _disposables)
                        d.Dispose();
                    _disposables = null;
                }
            }

            public override Task<object> GetValueAsync()
            {
                return Task.FromResult((object)_value);
            }

            public override string ToInvokeString()
            {
                // TODO: Customize your Dashboard invoke string
                return $"{Encoding.UTF8.GetString(_value.Body)}";
            }
        }

        private class ServiceBusQueueTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                // TODO: Customize your Dashboard display string
                return $"ServiceBusQueue trigger fired at {DateTime.Now:o}";
            }
        }
    }
}
