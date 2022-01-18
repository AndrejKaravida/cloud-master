﻿using Common;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebClient.Controllers
{
    public class HomeController : Controller
    {
        public List<Ticket> activeTickets = new List<Ticket>();
        public List<Ticket> historyTickets = new List<Ticket>();

        public HomeController()
        {
            ViewBag.activeTickets = activeTickets;
            ViewBag.historyTickets = historyTickets;
        }

        private Task CreateConsumer()
        {
            var factory = new ConnectionFactory { Uri = new Uri("amqp://guest:guest@localhost:5672") };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare("demo-queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            while (true)
            {
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (sender, e) =>
                {
                    var body = e.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Debug.WriteLine(message);
                };

                channel.BasicConsume("demo-queue", true, consumer);
                Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RefreshSummary()
        {
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
                    ViewBag.activeTickets = tickets;
                }

                var myBinding = new NetTcpBinding(SecurityMode.None);
                var myEndpoint = new EndpointAddress("net.tcp://localhost:56001/HistoryServiceEndpoint");

                using (var myChannelFactory = new ChannelFactory<IHistoryStatelessMethods>(myBinding, myEndpoint))
                {
                    try
                    {
                        var client = myChannelFactory.CreateChannel();
                        var tickets = await client.GetAllHistoryTickets();
                        ViewBag.historyTickets = tickets;

                        ((ICommunicationObject)client).Close();
                        myChannelFactory.Close();
                    }
                    catch (Exception e)
                    {

                    }
                }
            }
            catch (Exception ex) { }

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddTicket(string transportationType, DateTime departureDate, DateTime returnDate)
        {
            Ticket ticket = new Ticket()
            {
                Id = new Random().Next(1, 10000),
                TransportationType = transportationType,
                PurchaseDate = DateTime.UtcNow.AddSeconds(30),
                DepartureTime = departureDate,
                ReturnTime = returnDate
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
            }
            catch (Exception ex) { }

            return await RefreshSummary();
        }
    }
}
