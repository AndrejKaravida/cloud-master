using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IHistoryStatelessMethods
    {
        [OperationContract]
        Task<List<Ticket>> GetAllHistoryTickets();
    }
}
