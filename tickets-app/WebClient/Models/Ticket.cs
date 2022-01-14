using System;
using WebClient.Enums;

namespace WebClient.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public ETransportationType TransportationType { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime? ReturnTime { get; set; }
    }
}
