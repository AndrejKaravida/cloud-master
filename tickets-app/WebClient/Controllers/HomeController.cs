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
using System.Threading;
using System.Threading.Tasks;

namespace WebClient.Controllers
{
    public class HomeController : Controller
    {
        private static ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>> statefullService;
        private static ServicePartitionClient<WcfCommunicationClient<IHistoryStatelessMethods>> historyStatelessService;
        public List<Ticket> activeTickets = new List<Ticket>();
        public List<Ticket> historyTickets = new List<Ticket>();

        public HomeController()
        {
            ViewBag.activeTickets = activeTickets;
            ViewBag.historyTickets = historyTickets;
            OpenConnectionToStatefull();
            OpenConnectionToHistoryStateless();
        }

        public IActionResult Index()
        {
            return View();
        }

        private async void OpenConnectionToStatefull()
        {
            try
            {
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/TicketsStateful"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();

                    statefullService = new ServicePartitionClient<WcfCommunicationClient<IStatefulMethods>>(
                         new WcfCommunicationClientFactory<IStatefulMethods>(clientBinding: binding),
                         new Uri("fabric:/tickets_app/TicketsStateful")
                         );
            }
            catch (Exception ex) { }
        }

        private async void OpenConnectionToHistoryStateless()
        {
            try
            {
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/tickets_app/StatelessHistory"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();

                historyStatelessService = new ServicePartitionClient<WcfCommunicationClient<IHistoryStatelessMethods>>(
                     new WcfCommunicationClientFactory<IHistoryStatelessMethods>(clientBinding: binding),
                     new Uri("fabric:/tickets_app/StatelessHistory"));
            }
            catch (Exception ex) { }
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveTickets()
        {
            try
            {
                var tickets = await statefullService.InvokeWithRetryAsync(client => client.Channel.GetAllTickets());
                ViewBag.activeTickets = tickets;

            }
            catch (Exception ex) { }

            return View("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryTickets()
        {
            try
            {
                var tickets = await historyStatelessService.InvokeWithRetryAsync(client => client.Channel.GetAllHistoryTickets());
                ViewBag.historyTickets = tickets;

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
                PurchaseDate = DateTime.UtcNow.AddSeconds(15),
                DepartureTime = departureDate,
                ReturnTime = returnDate
            };

            try
            {
                await statefullService.InvokeWithRetryAsync(client => client.Channel.AddTicket(ticket));

            }
            catch (Exception ex) { }

            return View("Index");
        }
    }
}
