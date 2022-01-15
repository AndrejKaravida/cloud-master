using Common;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;

namespace WebClient.Controllers
{
    public class HomeController : Controller
    {
        private static ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> servicePartitionClient;
        public List<Ticket> activeTickets = new List<Ticket>();


        public HomeController()
        {
            ViewBag.activeTickets = activeTickets;
            OpenConnection();
        }

        public IActionResult Index()
        {
            return View();
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


        [HttpGet]
        public async Task<IActionResult> GetActiveTickets()
        {
            try
            {
                var tickets = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());
                ViewBag.activeTickets = tickets;

            } 
            catch(Exception ex) { }

            return View("Index");
        }

        [HttpGet]
        public IActionResult GetHistoryTickets()
        {
            //pozvati history database i uzeti sve arhivirane karte i vratiti na html

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddTicket(string transportationType, DateTime departureDate, DateTime returnDate)
        {
            Ticket ticket = new Ticket();
            ticket.Id = new Random().Next(1, 10000);
            ticket.TransportationType = transportationType;
            ticket.DepartureTime = departureDate;
            ticket.ReturnTime = returnDate;

            try
            {
                await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.AddTicket(ticket));
            } catch(Exception ex) { }

            return View("Index");
        }
    }
}
