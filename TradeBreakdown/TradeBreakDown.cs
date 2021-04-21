using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;

namespace TradeBreakdown
{
    public class TradeBreakdownSA
    {
        private class ClientOrderDistribution : ClientOrder
        {
            private double currentTotalAmount;

            public int TotalAllocatedToClient { get; private set; }
            public double CurrentAveragePrice { get; private set; }
            public double ClientPercentage { get; }
            public Dictionary<int, AllocatedTrade> ClientTradesList { get; }
            public Dictionary<int, int> AllocateByTrade { get; private set; }
            public ClientOrderDistribution(ClientOrder cliOrder, double clientPercentage) : base(cliOrder.CliendID, cliOrder.SolicitedShares)
            {
                this.ClientPercentage = clientPercentage;
                ClientTradesList = new Dictionary<int, AllocatedTrade>();
                this.AllocateByTrade = new Dictionary<int, int>();
            }

            public void AddTradeToClient(AllocatedTrade trade, int allocQuantity)
            {
                if (!ClientTradesList.ContainsKey(trade.TradeID))
                {
                    ClientTradesList.Add(trade.TradeID, trade);
                    AllocateByTrade.Add(trade.TradeID, 0);
                }

                AllocateTrade(trade, allocQuantity);
            }

            internal void RemoveTrade(AllocatedTrade trade, int allocQuantity)
            {
                allocQuantity = -Math.Abs(allocQuantity); //sanitze input

                if (!ClientTradesList.ContainsKey(trade.TradeID))
                    throw new Exception("Trying to remove a trade that doesnt exists");

                AllocateTrade(trade, allocQuantity);
            }

            private void AllocateTrade(AllocatedTrade trade, int allocQuantity)
            {
                trade.SetAllocatedQuantity(allocQuantity);
                this.TotalAllocatedToClient += allocQuantity;

                if (TotalAllocatedToClient > this.SolicitedShares)
                {
                    throw new Exception("Allocating more than asked");
                }

                AllocateByTrade[trade.TradeID] += allocQuantity;

                UpdateAveragePrice(trade, allocQuantity);
            }

            private void UpdateAveragePrice(AllocatedTrade trade, int allocQuantity)
            {
                if (this.TotalAllocatedToClient > 0)
                {
                    this.currentTotalAmount += (allocQuantity * trade.Price);
                    this.CurrentAveragePrice = this.currentTotalAmount / this.TotalAllocatedToClient;
                }
                else
                {
                    this.currentTotalAmount = 0;
                    this.CurrentAveragePrice = 0;
                }
            }

            public ClientOrderDistribution DeepCopy()
            {
                var copy = new ClientOrderDistribution(this, this.ClientPercentage);

                foreach (var trade in this.ClientTradesList.Values.ToList())
                {
                    copy.ClientTradesList.Add(trade.TradeID, trade.GetDeepCopy());
                }

                copy.AllocateByTrade = new Dictionary<int, int>(this.AllocateByTrade);
                copy.CurrentAveragePrice = this.CurrentAveragePrice;
                copy.currentTotalAmount = this.currentTotalAmount;
                copy.TotalAllocatedToClient = this.TotalAllocatedToClient;

                return copy;
            }

            internal void ApplyMinTradeSize(int minTradeSize)
            {
                foreach (var trade in this.AllocateByTrade.Keys)
                {
                    this.AllocateByTrade[trade] *= minTradeSize;
                    this.ClientTradesList[trade].ApplyMinTradeSize(minTradeSize);
                }
            }
        }
        private class AllocatedTrade : Trade
        {
            public int AllocatedQuantity { get; private set; }
            public int LeftQuantity { get; private set; }
            public AllocatedTrade(Trade trade, int allocatedQuantity) : base(trade.TradeID, trade.TotalQuantity, trade.Price)
            {
                this.AllocatedQuantity = allocatedQuantity;
                this.LeftQuantity = this.TotalQuantity - this.AllocatedQuantity;
            }

            public void SetAllocatedQuantity(int allocQuantity)
            {
                this.AllocatedQuantity += allocQuantity;
                this.LeftQuantity -= allocQuantity;

                if (LeftQuantity < 0)
                    throw new Exception("Trying to alloc more than it is possible");
            }

            public override AllocatedTrade GetDeepCopy()
            {
                var trade = new AllocatedTrade(this, this.AllocatedQuantity);
                return trade;
            }

            internal void ApplyMinTradeSize(int minTradeSize)
            {
                this.AllocatedQuantity *= minTradeSize;
                this.TotalQuantity *= minTradeSize;
                this.LeftQuantity *= minTradeSize;
            }
        }

        public enum BreakDownOptions { FastSwap, RandomSwap }

        private const double ACCEPTABLE_ERROR = 0;

        private readonly double startTemperature;
        private readonly double coolingFactor;
        private readonly Random randomGenerator;

        private readonly BreakDownOptions swapOption;


        public TradeBreakdownSA(int randomSeed, double startTemperature = 1100, double coolingFactor = 0.995, BreakDownOptions swapOption = BreakDownOptions.RandomSwap)
        {
            this.randomGenerator = new Random(randomSeed);
            this.startTemperature = startTemperature;
            this.coolingFactor = coolingFactor;
            this.swapOption = swapOption;
        }

        public TradeBreakdownSA(double startTemperature = 1100, double coolingFactor = 0.995, BreakDownOptions swapOption = BreakDownOptions.RandomSwap) : this(Environment.TickCount, startTemperature, coolingFactor, swapOption)
        {
        }

        public Dictionary<int/*clientID*/, Dictionary<int, Trade>/*TradeID*/> GetBreakdownFor(Dictionary<int, ClientOrder> clientOrders, Dictionary<int, Trade> tradesDict, out double bestSlippage, int minTradeSize = 1)
        {
            AssertParameters(clientOrders, tradesDict, minTradeSize);

            var tradeList = tradesDict.Values.ToList();

            if (minTradeSize > 1)
                ApplyMinTradeSize(clientOrders, tradesDict, minTradeSize);

            GetTradesInformation(tradeList, out int totalOrderQuantity, out double orderAveragePrice);

            List<ClientOrderDistribution> lstClientDistribution = GetClientDistributionList(clientOrders, totalOrderQuantity);

            var oldGcSettings = GCSettings.LatencyMode;
            GCSettings.LatencyMode = System.Runtime.GCLatencyMode.LowLatency;

            var currentSolution = GenerateBaseSolution(Clone(lstClientDistribution), tradesDict.Values.ToList());
            double currentSlippage = GetSolutionSlippage(currentSolution, orderAveragePrice);

            bestSlippage = currentSlippage;
            var bestSolution = currentSolution;

            //Simulated Annealing logic (not in a separeted function due performance reason)
            for (double currentTemp = startTemperature; currentTemp > 1; currentTemp *= coolingFactor)
            {
                var newSolution = currentSolution; //reference by performance reasons (1)

                GetRandomParametersToSwap(tradeList, newSolution, out var client1, out var client2, out var trade1, out var trade2);

                Swap(client1, trade1, client2, trade2);

                var newSlippage = GetSolutionSlippage(newSolution, orderAveragePrice);

                if (ShouldAcceptNewSolution(newSlippage, currentSlippage, currentTemp))
                {
                    //currentSolution = newSolution;// don't need to do it because already working on the reference object
                    currentSlippage = GetSolutionSlippage(currentSolution, orderAveragePrice);
                }
                else // since it is reference if I dont accept I must return to previous information
                {
                    FastSwap(client1, trade2, client2, trade1);
                }

                if (newSlippage < bestSlippage)
                {
                    bestSolution = ClonaSolutionList(newSolution);
                    bestSlippage = newSlippage;
                }

                if (bestSlippage <= ACCEPTABLE_ERROR)
                {
                    break;
                }
            }

            if (minTradeSize > 1)
                ApplyMinTradeSize(bestSolution, minTradeSize);

            GCSettings.LatencyMode = oldGcSettings;

            return GetSolutionFromClientDistribution(bestSolution);
        }

        private static void ApplyMinTradeSize(List<ClientOrderDistribution> bestSolution, int minTradeSize)
        {
            foreach (var cli in bestSolution)
            {
                cli.ApplyMinTradeSize(minTradeSize);
            }
        }

        private static void ApplyMinTradeSize(Dictionary<int, ClientOrder> clientOrders, Dictionary<int, Trade> tradesDict, int minTradeSize)
        {
            foreach (var item in clientOrders.Values)
            {
                item.SolicitedShares /= minTradeSize;
            }
            foreach (var item in tradesDict.Values)
            {
                item.TotalQuantity /= minTradeSize;
            }
        }

        private static void AssertParameters(Dictionary<int, ClientOrder> clientOrders, Dictionary<int, Trade> tradesDict, int minTradeSize)
        {
            if (minTradeSize < 1)
                throw new Exception("MinTradeSize must be equals or greater than 1");

            int totalFromTrades = 0;
            foreach (var tradeKVP in tradesDict)
            {
                if (tradeKVP.Key != tradeKVP.Value.TradeID)
                    throw new Exception($"Trade key = from dict {tradeKVP.Key} <> from entity = {tradeKVP.Value.TradeID}");

                if (tradeKVP.Value.TotalQuantity % minTradeSize != 0)
                    throw new Exception($"Trade {tradeKVP.Key} is not divisible by the mintradeSize {minTradeSize}. TradeQtty = {tradeKVP.Value.TotalQuantity}");

                totalFromTrades += tradeKVP.Value.TotalQuantity;
            }

            int totalFromClients = 0;
            foreach (var cliKVP in clientOrders)
            {
                if (cliKVP.Key != cliKVP.Value.CliendID)
                    throw new Exception($"Client key = from dict {cliKVP.Key} <> from entity = {cliKVP.Value.CliendID}");
                totalFromClients += cliKVP.Value.SolicitedShares;
            }

            if (totalFromClients != totalFromTrades)
                throw new Exception($"Clients asked for {totalFromClients} but trade sum = {totalFromTrades}");

        }

        private bool ShouldAcceptNewSolution(double newSlippage, double currentSlippage, double temperature)
        {
            return (newSlippage < currentSlippage) || Math.Exp((currentSlippage - newSlippage) / temperature) > randomGenerator.NextDouble();
        }

        private static void FastSwap(ClientOrderDistribution client1, int tradeID1, ClientOrderDistribution client2, int tradeID2)
        {
            var allocQuantity = Math.Min(client1.AllocateByTrade[tradeID1], client2.AllocateByTrade[tradeID2]);

            AllocatedTrade tradeForClient1 = client1.ClientTradesList[tradeID1];
            AllocatedTrade tradeForClient2 = client1.ClientTradesList[tradeID2];

            client1.RemoveTrade(tradeForClient1, allocQuantity); //trade is reference here
            client2.RemoveTrade(tradeForClient2, allocQuantity); //trade is reference here

            client1.AddTradeToClient(tradeForClient2, allocQuantity); //trade is reference here
            client2.AddTradeToClient(tradeForClient1, allocQuantity); //trade is reference here
        }

        private void Swap(ClientOrderDistribution client1, int tradeID1, ClientOrderDistribution client2, int tradeID2)
        {
            int allocQuantity = 0;
            if (client1.AllocateByTrade.TryGetValue(tradeID1, out int allocatedForClient1) && client2.AllocateByTrade.TryGetValue(tradeID2, out int allocatedForClient2))
            {
                allocQuantity = Math.Min(allocatedForClient1, allocatedForClient2);
            }

            if (allocQuantity == 0)
                return;

            if (swapOption == BreakDownOptions.RandomSwap)
                allocQuantity = this.randomGenerator.Next(1, allocQuantity);

            AllocatedTrade tradeForClient1 = client1.ClientTradesList[tradeID1];
            AllocatedTrade tradeForClient2 = client1.ClientTradesList[tradeID2];

            client1.RemoveTrade(tradeForClient1, allocQuantity); //trade is reference here
            client2.RemoveTrade(tradeForClient2, allocQuantity); //trade is reference here

            client1.AddTradeToClient(tradeForClient2, allocQuantity); //trade is reference here
            client2.AddTradeToClient(tradeForClient1, allocQuantity); //trade is reference here
        }

        private void GetRandomParametersToSwap(List<Trade> tradeList, List<ClientOrderDistribution> clientList, out ClientOrderDistribution client1, out ClientOrderDistribution client2, out int trade1, out int trade2)
        {
            var clientPosition1 = randomGenerator.Next(0, clientList.Count);
            var clientPosition2 = randomGenerator.Next(0, clientList.Count);
            if (clientPosition1 == clientPosition2)
                clientPosition2 = (clientPosition2 + 1) % clientList.Count;

            client1 = clientList[clientPosition1];
            client2 = clientList[clientPosition2];

            var tradePosition1 = randomGenerator.Next(0, tradeList.Count);
            var tradePosition2 = randomGenerator.Next(0, tradeList.Count);
            if (tradePosition1 == tradePosition2)
                tradePosition2 = (tradePosition2 + 1) % tradeList.Count;

            trade1 = tradeList[tradePosition1].TradeID;
            trade2 = tradeList[tradePosition2].TradeID;
        }

        private static List<ClientOrderDistribution> GenerateBaseSolution(List<ClientOrderDistribution> clientDistributionList, List<Trade> tradesList)
        {
            var allocatedTradesList = new List<AllocatedTrade>();

            //allocate proportinal
            foreach (var trade in tradesList)
            {
                var currentAllocatedTrade = new AllocatedTrade(trade, 0);
                allocatedTradesList.Add(currentAllocatedTrade);

                foreach (var cliOrder in clientDistributionList)
                {
                    int allocQuantity = Math.Min((int)(trade.TotalQuantity * cliOrder.ClientPercentage), trade.TotalQuantity);
                    cliOrder.AddTradeToClient(currentAllocatedTrade, allocQuantity); //trade reference here
                }
            }

            //allocate the leftovers
            foreach (var cliOrder in clientDistributionList)
            {
                var missingSharesToClient = cliOrder.SolicitedShares - cliOrder.TotalAllocatedToClient;

                if (missingSharesToClient == 0)
                    continue;

                foreach (var trade in allocatedTradesList)
                {
                    if (trade.LeftQuantity == 0)
                        continue;

                    var possibleToAllocate = Math.Min(missingSharesToClient, trade.LeftQuantity);

                    cliOrder.AddTradeToClient(trade, possibleToAllocate); //trade is reference here

                    missingSharesToClient = cliOrder.SolicitedShares - cliOrder.TotalAllocatedToClient;

                    if (missingSharesToClient == 0)
                        break;
                }
            }

            return clientDistributionList;
        }

        private static List<ClientOrderDistribution> ClonaSolutionList(List<ClientOrderDistribution> currentSolution)
        {
            var solution = new List<ClientOrderDistribution>();
            foreach (var item in currentSolution)
            {
                solution.Add(item.DeepCopy());
            }
            return solution;
        }

        private static Dictionary<int, Dictionary<int, Trade>> GetSolutionFromClientDistribution(List<ClientOrderDistribution> clientDistributionList)
        {
            var solution = new Dictionary<int, Dictionary<int, Trade>>();
            foreach (var clientDistribution in clientDistributionList)
            {
                var dictTrades = new Dictionary<int, Trade>();
                foreach (var trade in clientDistribution.ClientTradesList.Values.ToList())
                {
                    var allocated = clientDistribution.AllocateByTrade[trade.TradeID];

                    if (allocated == 0)
                        continue;

                    dictTrades.Add(trade.TradeID, new Trade(trade.TradeID, allocated, trade.Price));
                }
                solution.Add(clientDistribution.CliendID, dictTrades);
            }
            return solution;
        }

        private static double GetSolutionSlippage(List<ClientOrderDistribution> solution, double perfectAvgPrice)
        {
            double distanceFromPerfectAvg = 0;
            foreach (var clientTrades in solution)
            {
                distanceFromPerfectAvg += Math.Abs(perfectAvgPrice - clientTrades.CurrentAveragePrice);
            }

            return distanceFromPerfectAvg;
        }

        private static List<ClientOrderDistribution> GetClientDistributionList(Dictionary<int, ClientOrder> clientOrders, int totalOrderQuantity)
        {
            var lstClientDistribution = new List<ClientOrderDistribution>();

            var totalAskedForClients = 0;
            foreach (var cliOrder in clientOrders.Values)
            {
                totalAskedForClients += cliOrder.SolicitedShares;
                lstClientDistribution.Add(new ClientOrderDistribution(cliOrder, (double)cliOrder.SolicitedShares / totalOrderQuantity));
            }

            if (totalAskedForClients != totalOrderQuantity)
                throw new Exception($"Total Order Size={totalOrderQuantity} doesn't match with client distribution={totalAskedForClients}");

            return lstClientDistribution;
        }

        private static void GetTradesInformation(List<Trade> trades, out int totalquantity, out double averagePrice)
        {
            totalquantity = 0;
            averagePrice = 0;

            foreach (var trade in trades)
            {
                totalquantity += trade.TotalQuantity;
                averagePrice += (trade.TotalQuantity * trade.Price);
            }

            averagePrice /= totalquantity;
        }

        private static List<ClientOrderDistribution> Clone(List<ClientOrderDistribution> lstClientDistribution)
        {
            var clonedList = new List<ClientOrderDistribution>(lstClientDistribution.Count);
            foreach (var item in lstClientDistribution)
            {
                clonedList.Add(item.DeepCopy());
            }
            return clonedList;
        }
    }
}
