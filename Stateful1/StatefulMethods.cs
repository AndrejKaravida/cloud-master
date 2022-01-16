using Common;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace TicketsStateful
{
    class StatefulMethods : IStatefulMethods
    {

        private IReliableStateManager _stateManager;

        public StatefulMethods(IReliableStateManager stateManager)
        {
            this._stateManager = stateManager;
        }
        public async Task AddTicket(Ticket ticket)
        {
            var myDictionary = await this._stateManager.GetOrAddAsync<IReliableDictionary<int, Ticket>>("tickets");

            using (var tx = this._stateManager.CreateTransaction())
            {
                await myDictionary.AddOrUpdateAsync(tx, ticket.Id, ticket, (key, value) => value);

                await tx.CommitAsync();
            }
        }

        public async Task<List<Ticket>> GetAllTickets()
        {
            Dictionary<int, Ticket> toRet = new Dictionary<int, Ticket>();
            using (var tx = this._stateManager.CreateTransaction())
            {
                var myDictionary = await this._stateManager.GetOrAddAsync<IReliableDictionary<int, Ticket>>("tickets");
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<int, Ticket>> enumerable = await myDictionary.CreateEnumerableAsync(tx);
                using (Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<int, Ticket>> e = enumerable.GetAsyncEnumerator())
                {
                    while (await e.MoveNextAsync(new CancellationToken()).ConfigureAwait(false))
                    {
                        toRet.Add(e.Current.Key, e.Current.Value);
                    }
                }

                await tx.CommitAsync();
            }
            return toRet.Values.ToList();
        }

        public async Task RemoveTicketById(int id)
        {
            using (var tx = this._stateManager.CreateTransaction())
            {
                var myDictionary = await this._stateManager.GetOrAddAsync<IReliableDictionary<int, Ticket>>("tickets");
                await myDictionary.TryRemoveAsync(tx, id);

                await tx.CommitAsync();
            }
        }
    }
}
