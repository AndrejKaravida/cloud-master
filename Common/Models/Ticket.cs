using System;

namespace Common.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public string TransportationType { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime? ReturnTime { get; set; }
    }
}
