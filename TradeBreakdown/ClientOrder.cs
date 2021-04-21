using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeBreakdown
{
    public class ClientOrder
    {
        public int CliendID { get; }
        public int SolicitedShares { get; set; }
        public ClientOrder(int clientID, int solicitedShares)
        {
            this.CliendID = clientID;
            this.SolicitedShares = solicitedShares;
        }
    }
}
