using Common;
using Common.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatelessHistory
{
    class HistoryStatelessMethods : IHistoryStatelessMethods
    {
        private static IMongoCollection<Ticket> _mongoCollection;

        public HistoryStatelessMethods(IMongoCollection<Ticket> mongoCollection)
        {
            _mongoCollection = mongoCollection;
        }

        public async Task<List<Ticket>> GetAllHistoryTickets()
        {
            var tickets = await _mongoCollection.Find(_ => true).ToListAsync();
            return tickets;
        }
    }
}
