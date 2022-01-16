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
using System.Diagnostics;

namespace StatelessActive
{
    internal sealed class StatelessActive : StatelessService
    {
        private static ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> servicePartitionClient;

        public StatelessActive(StatelessServiceContext context)
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
            //return new List<ServiceInstanceListener>(1)
            //{
            //    new ServiceInstanceListener(context=> this.CreateWcfCommunicationListener(context), "ActiveServiceEndpoint")
            //};
        }

        //private ICommunicationListener CreateWcfCommunicationListener (StatelessServiceContext context)
        //{
        //    string host = context.NodeContext.IPAddressOrFQDN;
        //    var endpointConfig = context.CodePackageActivationContext.GetEndpoint("ActiveServiceEndpoint");
        //    int port = endpointConfig.Port;
        //    var scheme = endpointConfig.Protocol.ToString();
        //    string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/ActiveServiceEndpoint", scheme, host, port);

        //    var listener = new WcfCommunicationListener<IActiveStatelessMethods>(
        //        serviceContext: context, 
        //        wcfServiceObject: this, 
        //        listenerBinding: WcfUtility.CreateTcpListenerBinding(),
        //        address: new EndpointAddress(uri)
        //    );

        //    return listener;
        //}

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var tickets = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Tickets count:", tickets.Count);
                    Debug.WriteLine("Count ", tickets.Count);

                } catch(Exception ex) { }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
