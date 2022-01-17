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
using Common.Models;
using System.ServiceModel;

namespace TicketsStateful
{
    internal sealed class TicketsStateful : StatefulService
    {
        public TicketsStateful(StatefulServiceContext context)
            : base(context)
        {
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



        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Ticket>>("tickets");
            await myDictionary.ClearAsync();

            cancellationToken.ThrowIfCancellationRequested();

            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:56002/ActiveServiceEndpoint");

            using (var myChannelFactory = new ChannelFactory<IActiveStatelessMethods>(myBinding, myEndpoint))
            {
                try
                {
                    var client = myChannelFactory.CreateChannel();

                    var ticketsFromDb = await client.GetAllActiveTickets();

                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        if (await myDictionary.GetCountAsync(tx) == 0)
                        {
                            foreach (var ticket in ticketsFromDb)
                            {
                                await myDictionary.AddOrUpdateAsync(tx, ticket.Id, ticket, (key, value) => value);
                            }
                        }

                        await tx.CommitAsync();
                    }

                    ((ICommunicationObject)client).Close();
                    myChannelFactory.Close();
                }
                catch (Exception e)
                {

                }
            }
        }
    }
}

