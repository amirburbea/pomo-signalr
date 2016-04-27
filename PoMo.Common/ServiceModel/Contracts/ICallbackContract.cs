﻿using System.ServiceModel;
using PoMo.Common.DataObjects;

namespace PoMo.Common.ServiceModel.Contracts
{
    [ServiceContract(Namespace = Namespace.Value)]
    public interface ICallbackContract : IHeartbeatContract
    {
        [OperationContract(IsOneWay = false)]
        void ReceiveTicks(string portfolioId, TickData[] data);
    }
}