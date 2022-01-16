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
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Client;
using Common.Models;

namespace TicketsStateful
{
    internal sealed class TicketsStateful : StatefulService
    {
        private static ServicePartitionClient<WcfCommunicationClient<IActiveStatelessMethods>>activeStatelessService;

        public TicketsStateful(StatefulServiceContext context)
            : base(context)
        {
            OpenConnectionToActiveStateless();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context =>
                {
                    return new WcfCommunicationListener<IStatefulMethods>(context, new StatefulMethods(this.StateManager), WcfUtility.CreateTcpListenerBinding(), "StatefulEndpoint");
                }, "StatefulEndpoint")
            };
        }
        private async void OpenConnectionToActiveStateless()
        {
            try
            {
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/StatelessActive"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();
                int index = 0;

                for (int i = 0; i < partitionsNumber; i++)
                {
                    activeStatelessService = new ServicePartitionClient<WcfCommunicationClient<IActiveStatelessMethods>>(
                         new WcfCommunicationClientFactory<IActiveStatelessMethods>(clientBinding: binding),
                         new Uri("fabric:/tickets_app/StatelessActive"),
                         new ServicePartitionKey(index % partitionsNumber));
                    index++;
                }
            }
            catch (Exception ex) { }
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Ticket>>("tickets");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ticketsFromDb = await activeStatelessService.InvokeWithRetryAsync(client => client.Channel.GetAllActiveTickets());

                using (var tx = this.StateManager.CreateTransaction())
                {
                    if(await myDictionary.GetCountAsync(tx) == 0)
                    {
                        foreach(var ticket in ticketsFromDb)
                        {
                            await myDictionary.AddOrUpdateAsync(tx, ticket.Id, ticket, (key, value) => value);
                        }
                    }

                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
