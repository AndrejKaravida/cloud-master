using Common;
using Common.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatelessActive
{
    public class ActiveStatelessMethods : IActiveStatelessMethods
    {
        private static IMongoCollection<Ticket> _mongoCollection;

        public ActiveStatelessMethods(IMongoCollection<Ticket> mongoCollection)
        {
            _mongoCollection = mongoCollection;
        }

        public async Task<List<Ticket>> GetAllActiveTickets()
        {
            var tickets = await _mongoCollection.Find(_ => true).ToListAsync();
            return tickets;
        }
    }
}
