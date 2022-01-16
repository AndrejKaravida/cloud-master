using Common.Models;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IStatefulMethods
    {
        [OperationContract]
        Task AddTicket(Ticket ticket);

        [OperationContract]
        Task<List<Ticket>> GetAllTickets();

        [OperationContract]
        Task RemoveTicketById(int id);
    }
}
