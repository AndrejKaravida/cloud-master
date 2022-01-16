using Common.Models;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IActiveStatelessMethods
    {
        [OperationContract]
        Task<List<Ticket>> GetAllActiveTickets();
    }
}
