using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IEmailMethods
    {
        [OperationContract]
        void SendEmail();
    }
}
