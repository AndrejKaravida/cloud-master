using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace TicketsStateful
{
    internal sealed class TicketsStateful : StatefulService
    {
        public TicketsStateful(StatefulServiceContext context)
            : base(context)
        { }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context =>
                {
                    return new WcfCommunicationListener<IStatefulMethods>(context, new StatefulMethods(this.StateManager), WcfUtility.CreateTcpListenerBinding(), "StatefulEndpoint");
                }, "StatefulEndpoint")

                //new ServiceReplicaListener(context =>
                //{
                //    return new WcfCommunicationListener<IActiveStatelessMethods>(context, new ActiveStatelessMethods(this.StateManager), WcfUtility.CreateTcpListenerBinding(), "ActiveStatelessEndpoint");
                //}, "ActiveStatelessEndpoint")
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("ticfdsfdskets");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
