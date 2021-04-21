using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using TradeBreakdown;

namespace TestTradeBreakdown
{
    [TestClass]
    public class TestTradeBreakdown
    {
        [TestMethod]
        public void TestTradeBreakdownMethod()
        {
            var result = new TradeBreakdownSA().GetBreakdownFor(GetClientOrders(), GetTrades(), out double slippage);

            Assert.AreEqual(0, slippage);
            Assert.IsTrue(result.Count == 2);
        }

        private Dictionary<int, Trade> GetTrades()
        {
            var dictT = new Dictionary<int, Trade>();
            dictT.Add(1, new Trade(1, 100, 10));
            dictT.Add(2, new Trade(2, 100, 11));
            dictT.Add(3, new Trade(3, 100, 12));
            return dictT;
        }

        private Dictionary<int, ClientOrder> GetClientOrders()
        {
            var dictClis = new Dictionary<int, ClientOrder>();
            dictClis.Add(1, new ClientOrder(1, 250));
            dictClis.Add(2, new ClientOrder(2, 50));
            return dictClis;
        }
    }
}

