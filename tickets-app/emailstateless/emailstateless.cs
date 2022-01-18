using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using AE.Net.Mail;
using System.Diagnostics;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Common;
using Microsoft.ServiceFabric.Services.Client;

namespace emailstateless
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class emailstateless : StatelessService
    {
        static ImapClient IC;

        public emailstateless(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            long iterations = 0;

            while (true)
            {
                IC = new ImapClient("imap.gmail.com", "cloudtestmaster@gmail.com", "qujsjfelqbjeaoiw", AuthMethods.Login, 993, true);
                IC.SelectMailbox("INBOX");
                MailMessage email = IC.GetMessage(0);

                if (email != null)
                {
                    var entries = email.Body.Split(',');
                    var transporationType = entries[0];
                    var startingDate = entries[1];
                    var returningDate = entries[2];
                    returningDate = returningDate.Substring(0, returningDate.Length - 4);

                    Ticket ticket = new Ticket()
                    {
                        PurchaseDate = DateTime.UtcNow.AddSeconds(30),
                        TransportationType = transporationType,
                        DepartureTime = DateTime.Parse(startingDate),
                        ReturnTime = DateTime.Parse(returningDate),
                        Id = new Random().Next(1, 10000)
                    };

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
                            await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.AddTicket(ticket));
                        }
                        IC.DeleteMessage(email);
                    }
                    catch (Exception ex) { }
                }

                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
