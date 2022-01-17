using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Common;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Client;
using MongoDB.Driver;
using Common.Models;
using System.Globalization;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using System.ServiceModel;

namespace StatelessActive
{
    internal sealed class StatelessActive : StatelessService
    {
        private static IMongoCollection<Ticket> mongoCollection;

        public StatelessActive(StatelessServiceContext context)
            : base(context)
        {
            OpenDbConnection();
        }

        private void OpenDbConnection()
        {
            var settings = MongoClientSettings.FromConnectionString("mongodb+srv://admin:admin123@energyweathercluster.lkam4.mongodb.net/ticketsDatabase?retryWrites=true&w=majority");
            var client = new MongoClient(settings);
            mongoCollection = client.GetDatabase("ticketsDatabase").GetCollection<Ticket>("active");
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>(1)
            {
                new ServiceInstanceListener(context=> this.CreateWcfCommunicationListener(context), "ActiveServiceEndpoint")
            };
        }

        private ICommunicationListener CreateWcfCommunicationListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("ActiveServiceEndpoint");
            int port = endpointConfig.Port;
            var scheme = endpointConfig.Protocol.ToString();
            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/ActiveServiceEndpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IActiveStatelessMethods>(
                serviceContext: context,
                wcfServiceObject: new ActiveStatelessMethods(mongoCollection),
                listenerBinding: WcfUtility.CreateTcpListenerBinding(),
                address: new EndpointAddress(uri)
            );

            return listener;
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    FabricClient fabricClient = new FabricClient();
                    int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/TicketsStateful"))).Count;
                    var binding = WcfUtility.CreateTcpClientBinding();
                    int index = 0;

                    for (int i = 0; i < partitionsNumber; i++)
                    {
                        ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>>(
                             new WcfCommunicationClientFactory<IStatefulMethods>(clientBinding: binding),
                             new Uri("fabric:/tickets_app/TicketsStateful"),
                             new ServicePartitionKey(index % partitionsNumber)
                             );

                        var tickets = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());

                        foreach (var ticket in tickets)
                        {
                            var existingTicket = await mongoCollection.Find(x => x.Id == ticket.Id).SingleOrDefaultAsync();
                            if (existingTicket == null)
                            {
                                await mongoCollection.InsertOneAsync(ticket);
                            }
                        }
                    }
                }
                catch (Exception ex) { }

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }
}
