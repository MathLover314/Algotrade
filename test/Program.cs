﻿using System;
using Alpaca.Markets;
using System.Threading.Tasks;
using System.Text;
using System.Data;
using ScottPlot;
using System.Drawing;
using Microsoft.VisualBasic;
using System.Runtime.CompilerServices;

namespace test
{
    internal static class Program
    {
        private const String KEY_ID = "";

        private const String SECRET_KEY = "";
        public static async Task Main()
        {
            var key = new SecretKey(KEY_ID, SECRET_KEY);
            var client = Environments.Paper
                .GetAlpacaTradingClient(key);
            var symbol = "BTC/USD";
            var clock = await client.GetClockAsync();
            var account = await client.GetAccountAsync();

            if (account.IsTradingBlocked)
            {
                Console.WriteLine("Account is currently restricted from trading.");
            }

            if (clock != null)
            {
                Console.WriteLine($"Timestamp: ${clock.TimestampUtc}, NextOpen: ${clock.NextOpenUtc}, NextClose: ${clock.NextCloseUtc}");
            }
            
            var balance_change = account.Equity - account.LastEquity;

            Console.WriteLine($"Today's portfolio balance change: ${balance_change}");
            //var ask = await client.GetAssetAsync(symbol.Remove('/'));
            var qty = 0.001M;
            await DrawPlot(clock, key, symbol);
            
            var ordrer = await client.PostOrderAsync(MarketOrder.Buy(symbol.Replace("/", string.Empty), OrderQuantity.Fractional(qty)).WithDuration(TimeInForce.Gtc));
            var symbolPosition = await client.GetPositionAsync(symbol.Replace("/", string.Empty));
            for (int i = 0; i<30; i++) {
                
                var lastPrice = symbolPosition.AssetCurrentPrice;
                int iter = 0;
                while(lastPrice >= symbolPosition.AssetCurrentPrice) {
                    if(iter>2) {
                        iter = 0;
                        lastPrice = symbolPosition.AssetCurrentPrice;
                    }
                    symbolPosition = await client.GetPositionAsync(symbol.Replace("/", string.Empty));
                    System.Threading.Thread.Sleep(300000);
                    iter++;
                    Console.WriteLine($"The price went: {symbolPosition.AssetCurrentPrice}");
                }
                var order = await client.PostOrderAsync(MarketOrder.Buy(symbol, OrderQuantity.Fractional(qty)).WithDuration(TimeInForce.Gtc));
                await DrawPlot(clock, key, symbol);
                lastPrice = symbolPosition.AssetCurrentPrice;
                Console.WriteLine($"Buy price: {symbolPosition.AssetCurrentPrice}");
                while(lastPrice <= symbolPosition.AssetCurrentPrice) {
                    lastPrice = symbolPosition.AssetCurrentPrice;
                    symbolPosition = await client.GetPositionAsync(symbol.Replace("/", string.Empty));
                    System.Threading.Thread.Sleep(300000);
                    Console.WriteLine($"The price went: {symbolPosition.AssetCurrentPrice}");
                }
                Console.WriteLine($"Sell price: {symbolPosition.AssetCurrentPrice}");
                order = await client.PostOrderAsync(
                    MarketOrder.Sell(symbol, OrderQuantity.Fractional(symbolPosition.Quantity-(decimal)0.0009978)).WithDuration(TimeInForce.Gtc));
            }
        }

        public static async Task DrawPlot(IClock clock, SecretKey key, string symbol) {
             var data_client = Environments.Paper
                .GetAlpacaCryptoDataClient(key);
            var into = clock.TimestampUtc.AddMinutes(-15);
            var from = into.AddDays(-4);
            var bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, from, into, BarTimeFrame.Hour)
            );

            var upper = new List<double>();
            var lower = new List<double>();
            var mean = new List<double>();
            var times = new List<DateTime>();

            foreach(var bar in bars.Items){
                upper.Add((double)bar.High);
                lower.Add((double)bar.Low);
                mean.Add((double)(bar.High+bar.Low)/2);
                times.Add(bar.TimeUtc);
            }
            var index = Enumerable.Range(1, times.Count).Select(x => (double)x/times.Count).ToList();
            List<double> coefficients = Regression.PolyRegression(index, mean,8);
            List<double> differential = Enumerable.Range(1, times.Count-1).Select(x => (double)x).ToList();

            for(int i = 0; i < coefficients.Count-1; i++) {
                differential[i] = differential[i] * coefficients[i+1];
            }

            var estimation = new List<double>();
            foreach(var i in index) {
                estimation.Add(Regression.PolyFunction(coefficients, i));
            }

            var plot = new ScottPlot.Plot();
            var convbars = new List<ScottPlot.OHLC>();
            foreach(var bar in bars.Items) {
                convbars.Add(new ScottPlot.OHLC((double)bar.Open, (double)bar.High, (double)bar.Low, (double)bar.Close, bar.TimeUtc, TimeSpan.FromHours(1)));
            }


            plot.Add.Candlestick(convbars);
            //plot.Add.Scatter(times, upper);
            //plot.Add.Scatter(times, lower);
            plot.Add.Scatter(times, estimation);
            plot.Add.Scatter(times, mean);
            plot.Axes.DateTimeTicksBottom();

            plot.SavePng("scatter.png",1200, 700);
        }
    }

}