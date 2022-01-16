using Common;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace StatelessHistory
{
    internal sealed class StatelessHistory : StatelessService
    {
        private static ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> servicePartitionClient;

        public StatelessHistory(StatelessServiceContext context)
            : base(context)
        {
            OpenConnection();
        }

        private async void OpenConnection()
        {
            try
            {
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/TicketsStateful"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();
                int index = 0;

                for (int i = 0; i < partitionsNumber; i++)
                {
                    servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>>(
                         new WcfCommunicationClientFactory<IStatefulMethods>(clientBinding: binding),
                         new Uri("fabric:/tickets_app/TicketsStateful"),
                         new ServicePartitionKey(index % partitionsNumber));
                    index++;
                }
            }
            catch (Exception ex) { }
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tickets = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());
                ServiceEventSource.Current.ServiceMessage(this.Context, "Tickets count:", tickets.Count);
                Debug.WriteLine("Count ", tickets.Count);

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
