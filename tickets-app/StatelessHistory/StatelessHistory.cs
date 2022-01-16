using Common;
using Common.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Globalization;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace StatelessHistory
{
    internal sealed class StatelessHistory : StatelessService
    {
        private static ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> servicePartitionClient;
        private static IMongoCollection<Ticket> mongoCollection;

        public StatelessHistory(StatelessServiceContext context)
            : base(context)
        {
            OpenConnection();
            OpenDbConnection();
        }

        private void OpenDbConnection()
        {

            var settings = MongoClientSettings.FromConnectionString("mongodb+srv://admin:admin123@energyweathercluster.lkam4.mongodb.net/ticketsDatabase?retryWrites=true&w=majority");
            var client = new MongoClient(settings);
            mongoCollection = client.GetDatabase("ticketsDatabase").GetCollection<Ticket>("history");
        }

        private async void OpenConnection()
        {
            try
            {
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/TicketsStateful"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();

                    servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>>(
                         new WcfCommunicationClientFactory<IStatefulMethods>(clientBinding: binding),
                         new Uri("fabric:/tickets_app/TicketsStateful"));
            }
            catch (Exception ex) { }
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>(1)
            {
                new ServiceInstanceListener(context=> this.CreateWcfCommunicationListener(context), "HistoryServiceEndpoint")
            };
        }

        private ICommunicationListener CreateWcfCommunicationListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("HistoryServiceEndpoint");
            int port = endpointConfig.Port;
            var scheme = endpointConfig.Protocol.ToString();
            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/HistoryServiceEndpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IHistoryStatelessMethods>(
                serviceContext: context,
                wcfServiceObject: new HistoryStatelessMethods(mongoCollection),
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
                    var tickets = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());

                    foreach(var ticket in tickets)
                    {
                        var existingTicket = await mongoCollection.Find(x => x.Id == ticket.Id).SingleOrDefaultAsync();
                        if (existingTicket == null)
                        {
                            if(ticket.PurchaseDate < DateTime.UtcNow)
                            {
                                await mongoCollection.InsertOneAsync(ticket);
                                await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.RemoveTicketById(ticket.Id));
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
