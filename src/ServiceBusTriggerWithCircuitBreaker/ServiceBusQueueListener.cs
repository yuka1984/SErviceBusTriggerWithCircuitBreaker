using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace ServiceBusTriggerWithCircuitBreaker
{
    public class ServiceBusQueueListener : IListener
    {
        private static readonly int breakErrorSpan = 60000;
        private static readonly int breakErrorCount = 5;
        private static readonly int minOpenTime = 60000;
        private static readonly int maxOpenTime = 300000;
        

        private object lockObj = new object();
        private DateTime? firstFaultTime = null;
        private DateTime lastFaultTime = DateTime.MinValue;
        private int openCount = 0;
        private int errorCount = 0;

        private IQueueClient _queueClient;
        private readonly Func<IQueueClient> _queueClientCreateFunc;
        private readonly ServiceBusTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;

        public ServiceBusQueueListener(ServiceBusQueueTriggerAttribute attribute, ServiceBusTriggerExecutor executor)
            : this(new Func<IQueueClient>(()=> new QueueClient(attribute.Connection, attribute.QueueName, ReceiveMode.PeekLock, RetryPolicy.Default)), executor)
        {
        }

        public ServiceBusQueueListener(Func<IQueueClient> queueClientFunc, ServiceBusTriggerExecutor executor)
        {
            _queueClientCreateFunc = queueClientFunc;
            _triggerExecutor = executor;
            _cancellationTokenSource = new CancellationTokenSource();
            _disposed = false;
        }
        /// <summary>アンマネージ リソースの解放またはリセットに関連付けられているアプリケーション定義のタスクを実行します。</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();

                if (_queueClient != null)
                {
                    _queueClient.CloseAsync().GetAwaiter().GetResult();
                    _queueClient = null;
                }

                _disposed = true;
            }
        }

        /// <summary>Start listening.</summary>
        /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param>
        /// <returns>A task that completes when the listener is fully started.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            _queueClient = _queueClientCreateFunc();
            _queueClient.RegisterMessageHandler(ProcessMessageHandler, new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                AutoComplete = false,

            });
            return Task.CompletedTask;
        }

        

        /// <summary>Stop listening.</summary>
        /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param>
        /// <returns>A task that completes when the listener has stopped.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (_queueClient == null)
            {
                throw new InvalidOperationException(
                    "The listener has not yet been started or has already been stopped.");
            }

            _cancellationTokenSource.Cancel();

            return StopAsyncCore(cancellationToken);
        }

        private async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _queueClient.CloseAsync();
            _queueClient = null;
        }

        /// <summary>Cancel any in progress listen operation.</summary>
        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        private async Task ProcessMessageHandler(Message message, CancellationToken cancellationToken)
        {
            if (firstFaultTime.HasValue && (lastFaultTime - firstFaultTime.Value) > TimeSpan.FromMilliseconds(breakErrorSpan))
            {
                firstFaultTime = null;
                errorCount = 0;
                openCount = 0;
            }

            var result = await _triggerExecutor.ExecuteAsync(message, cancellationToken);
            var lockToken = message.SystemProperties.IsLockTokenSet ? message.SystemProperties.LockToken : "";
            if (result.Succeeded)
            {
                await _queueClient.CompleteAsync(lockToken);
            }
            else
            {
                await _queueClient.AbandonAsync(lockToken);
                if (!firstFaultTime.HasValue)
                {
                    lock (lockObj)
                    {
                        if (!firstFaultTime.HasValue)
                        {
                            firstFaultTime = DateTime.Now;
                            errorCount = 0;
                        }
                    }
                }
                lastFaultTime = DateTime.Now;
                errorCount++;
                if (!_queueClient.IsClosedOrClosing  && errorCount > breakErrorCount)
                {
                    lock (lockObj)
                    {
                        if (!_queueClient.IsClosedOrClosing && errorCount > breakErrorCount)
                        {
                            _queueClient.CloseAsync().GetAwaiter().GetResult();

                            openCount++;
                            var delayTime = minOpenTime * openCount;
                            if (delayTime > maxOpenTime)
                            {
                                delayTime = maxOpenTime;
                            }

                            Task.Delay(delayTime).ContinueWith(task =>
                            {
                                errorCount = 0;
                                firstFaultTime = null;
                                StartAsync(new CancellationToken());
                            });
                        }
                    }
                }
            }            
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            return Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
