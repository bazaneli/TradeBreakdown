using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeBreakdown
{
    public class Trade
    {
        public int TradeID { get; }
        public int TotalQuantity { get; set; }
        public double Price { get; }

        public Trade(int tradeID, int quantity, double price)
        {
            this.TradeID = tradeID;
            this.TotalQuantity = quantity;
            this.Price = price;
        }

        public virtual Trade GetDeepCopy()
        {
            return new Trade(this.TradeID, this.TotalQuantity, this.Price);
        }
    }
}
