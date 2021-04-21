using System;
using System.Collections.Generic;
using System.Diagnostics;
using TradeBreakdown;

namespace TradeSplitterConsole
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("...Performance simulation...");

            var seed = Environment.TickCount;

            var trades = GetTrades(out int total);
            var clientsOrder = GetClientOrders(total);
            double bestSlippage = 0;

            var sp = new Stopwatch();

            Console.WriteLine("Warming up");
            for (int i = 0; i < 100; i++)
            {
                new TradeBreakdownSA(seed, 1000, 0.995, TradeBreakdownSA.BreakDownOptions.FastSwap).GetBreakdownFor(clientsOrder, trades, out bestSlippage);
                new TradeBreakdownSA(seed, 1000, 0.995, TradeBreakdownSA.BreakDownOptions.RandomSwap).GetBreakdownFor(GetClientOrders(total), trades, out bestSlippage);
            }

            Console.WriteLine("Using FastSwap");
            sp.Restart();
            for (int i = 0; i < 100; i++)
                new TradeBreakdownSA(seed, 1100, 0.995, TradeBreakdownSA.BreakDownOptions.FastSwap).GetBreakdownFor(clientsOrder, trades, out bestSlippage);
            sp.Stop();

            Console.WriteLine($"Best Slippage = {bestSlippage} time = {sp.ElapsedMilliseconds} ms for running 100x");


            Console.WriteLine("Using RandomSwap");
            sp.Restart();
            for (int i = 0; i < 100; i++)
                new TradeBreakdownSA(seed, 1100, 0.995, TradeBreakdownSA.BreakDownOptions.RandomSwap).GetBreakdownFor(clientsOrder, trades, out bestSlippage);
            sp.Stop();

            Console.WriteLine($"Best Slippage = {bestSlippage} time = {sp.ElapsedMilliseconds} ms for running 100x");

            Console.WriteLine("Using Increasing temperature/Cooling down slowly [FastSwap]");
            sp.Restart();
            for (int i = 0; i < 100; i++)
                new TradeBreakdownSA(seed, 11000, 0.999, TradeBreakdownSA.BreakDownOptions.FastSwap).GetBreakdownFor(clientsOrder, trades, out bestSlippage);
            sp.Stop();

            Console.WriteLine($"Best Slippage = {bestSlippage} time = {sp.ElapsedMilliseconds} ms");


            Console.WriteLine("Using Increasing temperature/Cooling down slowly [RandomSwap]");
            sp.Restart();
            for (int i = 0; i < 100; i++)
                new TradeBreakdownSA(seed, 11000, 0.999, TradeBreakdownSA.BreakDownOptions.FastSwap).GetBreakdownFor(clientsOrder, trades, out bestSlippage);
            sp.Stop();

            Console.WriteLine($"Best Slippage = {bestSlippage} time = {sp.ElapsedMilliseconds} ms");

            /////////////
            ///USE SUGESTION
            //////////////
            //Console.WriteLine("...USE SUGGESTION....");

            Dictionary<int, Dictionary<int, Trade>> myResult;

            var resultForFast = new TradeBreakdownSA(swapOption: TradeBreakdownSA.BreakDownOptions.FastSwap).GetBreakdownFor(clientsOrder, trades, out double fastBestSlippage);
            var resultForRandom = new TradeBreakdownSA(swapOption: TradeBreakdownSA.BreakDownOptions.RandomSwap).GetBreakdownFor(clientsOrder, trades, out double randomBestSlippage);

            myResult = fastBestSlippage < randomBestSlippage ? resultForFast : resultForRandom;

            /////////////
            ///USE SUGESTION
            //////////////

            Console.WriteLine("Press enter to close");
            Console.ReadLine();
        }

        private static Dictionary<int, Trade> GetTrades(out int total)
        {
            Dictionary<int, Trade> dictT = new Dictionary<int, Trade>();
            Random r = new Random();
            total = 0;
            for(int i=0; i<1000; i++)
            {
                int size = r.Next(100, 500);
                dictT.Add(i, new Trade(i, size, r.Next(10, 15))) ;
                total += size;
            }
            return dictT;
        }

        private static Dictionary<int, ClientOrder> GetClientOrders(int total)
        {
            Dictionary<int, ClientOrder> dictClis = new Dictionary<int, ClientOrder>();

            int qdeCada = total / 10;


            for (int i = 0; i < 9; i++)
                dictClis.Add(i, new ClientOrder(i, qdeCada));

            dictClis.Add(10, new ClientOrder(10, total - (qdeCada * 9)));

            return dictClis;
        }
    }
}
