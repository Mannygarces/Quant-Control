#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class LinRegCrossV5UL : Strategy
	{
		private LinRegIntercept LinRegIntercept2;
		private LinRegIntercept LinRegIntercept5;
		private LinRegIntercept LinRegIntercept6;
		private LinRegIntercept LinRegIntercept7;
		private LinRegIntercept LinRegIntercept8;
		private DateTime lastTradeTime = DateTime.MinValue;
		private double dailyProfit = 0;
        private bool tradingAllowed = true;
		private DateTime conditionsMetStartTime;
    	private bool conditionsMet = false;
			
		//trailing profit
		private int trailingProfit2Ticks = 250; // Default value
        private int trailingStopDistanceTicks = 30; // Default value
        private bool useTrailingProfit2 = false;
        private double lastTrailingStopLevel = 0;
				
		//Unathorized user control
		private bool isAuthorized = false;
		

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "LinRegCrossV5UL";
				Calculate									= Calculate.OnEachTick;
				EntriesPerDirection							= 1;
				EntryHandling 								= EntryHandling.UniqueEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= true;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 10;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;		
				//user inputs
				Qty1						= 1;
				Qty2						= 1;
				DailyProfitTarget			= 800;
				//DailyMaxLoss				= 3000; managed at the account level 		
				TakeProfit1					= 150;
				TakeProfit2					= 250;
				StopLoss1					= 80;
				StopLoss2					= 80;
				TargetCoolDown 				= 300;
				StopCoolDown				= 10;
				//trailing stop parameters
				TrailingProfit2Ticks = 250;
                TrailingStopDistanceTicks = 30;
                UseTrailingProfit2 = false;
		
			}
			else if (State == State.Configure)
			{
				  // List of hardcoded authorized Machine IDs
		        List<string> authorizedMachineIds = new List<string> { "2FE617939206E05EE23F8BE889B419AD", "E0351A6D59677612A2D88AA18BEC0321", "ID3" }; // Replace ID1, ID2, etc. with actual Machine IDs
		
		        // Get the current Machine ID
		        string currentMachineId = NinjaTrader.Cbi.License.MachineId;
		
		        // Check if the current Machine ID is authorized
		        isAuthorized = authorizedMachineIds.Contains(currentMachineId);
		
		        if (!isAuthorized)
		        {
		            // Log unauthorized access attempt
		            Log("Unauthorized access attempt by Machine ID: " + currentMachineId, LogLevel.Error);
		            Print("This Machine ID is not authorized to use this strategy with live data.");
		        }
			}
			else if (State == State.DataLoaded)
			{				
				LinRegIntercept2				= LinRegIntercept(Close, 2);
				LinRegIntercept5				= LinRegIntercept(Close, 5);
				LinRegIntercept6				= LinRegIntercept(Close, 6);
				LinRegIntercept7				= LinRegIntercept(Close, 7);
				LinRegIntercept8				= LinRegIntercept(Close, 8);
				LinRegIntercept2.Plots[0].Brush = Brushes.Blue;
				LinRegIntercept5.Plots[0].Brush = Brushes.FloralWhite;
				LinRegIntercept6.Plots[0].Brush = Brushes.Yellow;
				LinRegIntercept7.Plots[0].Brush = Brushes.Purple;
				LinRegIntercept8.Plots[0].Brush = Brushes.Red;
				AddChartIndicator(LinRegIntercept2);
				AddChartIndicator(LinRegIntercept5);
				AddChartIndicator(LinRegIntercept6);
				AddChartIndicator(LinRegIntercept7);
				AddChartIndicator(LinRegIntercept8);
				SetProfitTarget("LS1", CalculationMode.Ticks, TakeProfit1);
				SetProfitTarget("SS1", CalculationMode.Ticks, TakeProfit1);
				SetStopLoss("LS1", CalculationMode.Ticks, StopLoss1, true);
				SetStopLoss("SS1", CalculationMode.Ticks, StopLoss1, true);
				//SetProfitTarget("LS2", CalculationMode.Ticks, TakeProfit2);
				//SetProfitTarget("SS2", CalculationMode.Ticks, TakeProfit2);
				SetStopLoss("LS2", CalculationMode.Ticks, StopLoss2, true);
				SetStopLoss("SS2", CalculationMode.Ticks, StopLoss2, true);
				// Conditionally set secondary targets based on trailing stop usage
		        if (!UseTrailingProfit2) // Only set fixed profit targets if trailing is not used
		        {
		            SetProfitTarget("LS2", CalculationMode.Ticks, TakeProfit2);
		            SetProfitTarget("SS2", CalculationMode.Ticks, TakeProfit2);
		        }
				
			}
		}
		
		protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, Cbi.MarketPosition marketPosition, string orderId, DateTime time)
		{
		    if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
		    {
		        // Assuming order names are exactly "Profit target" and "Stop loss"
		        bool isProfitHit = execution.Order.OrderType == OrderType.Limit && execution.Order.Name.Contains("Profit");
		        bool isStopHit = execution.Order.OrderType == OrderType.StopMarket && execution.Order.Name.Contains("Stop");
		        HandleTradeExecution(isProfitHit, isStopHit);
		    }
		}



		protected override void OnBarUpdate()
		{
		    if (State == State.Historical || BarsInProgress != 0 || CurrentBars[0] < 1)
		        return;
		
		    if (Bars.IsFirstBarOfSession)
		    {
		        dailyProfit = 0;
		        tradingAllowed = true;
		        lastProfitTargetHit = DateTime.MinValue;
		        lastStopLoss1Hit = DateTime.MinValue;
		    }
		
		    if (!tradingAllowed || !isAuthorized)
		        return;
		
		    TimeSpan currentTime = Times[0][0].TimeOfDay;
			
		    // Close positions after the end time
		    if (currentTime < StartTime || currentTime > EndTime && Account.Positions.Count > 0) // Ensure 'Positions' is correctly referenced through the Account object
		    {
		        ClosePositions();
		       	Log($"Current time {currentTime} is outside trading hours ({StartTime} to {EndTime}).", LogLevel.Information);
		        return;
		    }
		
		    if (IsCoolingDown())
		    {
		        Log("Entry prevented due to cooling down.", LogLevel.Information);
		        return;
		    }
			
			
			 // Set 1
			if ((Position.MarketPosition != MarketPosition.Long)
				 && (tradingAllowed)
				 // Entry Sequence Long
				 && ((LinRegIntercept2[0] > LinRegIntercept5[0])
				 && (LinRegIntercept2[0] > LinRegIntercept6[0])
				 && (CrossAbove(LinRegIntercept2, LinRegIntercept7, 1))))
			{
				EnterLong(Convert.ToInt32(Qty1), @"LS1");
				if (Qty2 > 0)  // Check if Qty2 is greater than zero before placing the order
		        {
		            EnterLong(Convert.ToInt32(Qty2), @"LS2");
		        }
				HandleTradeExecution(false, false);
			}
			
			 // Set 2
			if ((Position.MarketPosition == MarketPosition.Long)
				 // Exit Sequence Long
				 && ((LinRegIntercept2[0] < LinRegIntercept5[0])
				 && (CrossBelow(LinRegIntercept2, LinRegIntercept6, 1))))
			{
				ExitLong(Convert.ToInt32(Qty1), @"ELS1", "LS1");
				if (Qty2 > 0)
	        {
	            ExitLong(Convert.ToInt32(Qty2), @"ELS2", "LS2");
	        }
				HandleTradeExecution(false, false);
			}
			
			 // Set 3
			if ((Position.MarketPosition != MarketPosition.Short)
				 && (tradingAllowed)
				 // Entry Sequence Short
				 && ((LinRegIntercept2[0] < LinRegIntercept5[0])
				 && (LinRegIntercept2[0] < LinRegIntercept6[0])
				 && (CrossBelow(LinRegIntercept2, LinRegIntercept7, 1))))
			{
				EnterShort(Convert.ToInt32(Qty1), @"SS1");
				 if (Qty2 > 0)
		        {
		            EnterShort(Convert.ToInt32(Qty2), @"SS2");
		        }
				HandleTradeExecution(false, false);
			}
			
			 // Set 4
			if ((Position.MarketPosition == MarketPosition.Short)
				 // Exit Sequence Short
				 && ((LinRegIntercept2[0] > LinRegIntercept5[0])
				 && (CrossAbove(LinRegIntercept2, LinRegIntercept6, 1))))
			{
				ExitShort(Convert.ToInt32(Qty1), @"ESS1", "SS1");
				if (Qty2 > 0)
		        {
		            ExitShort(Convert.ToInt32(Qty2), @"ESS2", "SS2");
		        }
				HandleTradeExecution(false, false);
			}
			
			 // Set 5
			if ((Position.MarketPosition == MarketPosition.Short)
				 && (CrossAbove(LinRegIntercept2, LinRegIntercept8, 1)))
			{
				ExitShort(Convert.ToInt32(Qty1), @"Force Short Exit", "SS1");
				if (Qty2 > 0)
		        {
				ExitShort(Convert.ToInt32(Qty2), @"Force Short Exit", "SS2");
				}
				HandleTradeExecution(false, false);
			}
			
			 // Set 6
			if ((Position.MarketPosition == MarketPosition.Long)
				 && (CrossBelow(LinRegIntercept2, LinRegIntercept8, 1)))
			{
				ExitLong(Convert.ToInt32(Qty1), @"Force Long Exit", "LS1");
				if (Qty2 > 0)
		        {
				ExitLong(Convert.ToInt32(Qty2), @"Force Long Exit", "LS2");
				}
				HandleTradeExecution(false, false);
			}
			// Check daily profit/loss
        	CheckDailyPerformance();
		// Implement trailing stop logic
		    if (UseTrailingProfit2 && Position.MarketPosition != MarketPosition.Flat)
		    {
		        AdjustTrailingStop();
		    }
		}
		
		private void ClosePositions()
		{
		    if (Position.MarketPosition == MarketPosition.Long)
		    {
		        ExitLong("EndOfDayExit", "LS1");
		        ExitLong("EndOfDayExit", "LS2");
		    }
		    else if (Position.MarketPosition == MarketPosition.Short)
		    {
		        ExitShort("EndOfDayExit", "SS1");
		        ExitShort("EndOfDayExit", "SS2");
			}
		}
		
		private void AdjustTrailingStop()
		{
		    try
		    {
		        double triggerPriceLong = Position.AveragePrice + (TakeProfit1 + TrailingProfit2Ticks) * TickSize;
		        double triggerPriceShort = Position.AveragePrice - (TakeProfit1 + TrailingProfit2Ticks) * TickSize;
		
		        if (Position.MarketPosition == MarketPosition.Long && Highs[0][0] >= triggerPriceLong)
		        {
		            double newStopPrice = Math.Max(Highs[0][0] - TrailingStopDistanceTicks * TickSize, lastTrailingStopLevel);
		            if (newStopPrice > lastTrailingStopLevel)
		            {
		                lastTrailingStopLevel = newStopPrice;
		                SetStopLoss("LS2", CalculationMode.Price, lastTrailingStopLevel, false);
		                DrawTrailingLine("trailingStopLong", lastTrailingStopLevel, Brushes.Red);
		            }
		        }
		        else if (Position.MarketPosition == MarketPosition.Short && Lows[0][0] <= triggerPriceShort)
		        {
		            double newStopPrice = Math.Min(Lows[0][0] + TrailingStopDistanceTicks * TickSize, lastTrailingStopLevel);
		            if (newStopPrice < lastTrailingStopLevel || lastTrailingStopLevel == 0)
		            {
		                lastTrailingStopLevel = newStopPrice;
		                SetStopLoss("SS2", CalculationMode.Price, lastTrailingStopLevel, false);
		                DrawTrailingLine("trailingStopShort", lastTrailingStopLevel, Brushes.Green);
		            }
		        }
		    }
		    catch (Exception ex)
		    {
		        Log("Error in AdjustTrailingStop: " + ex.Message, LogLevel.Error);
		    }
		}
	
		private void CheckDailyPerformance()
		{
		    // Get the total profit/loss in the account's currency
		    double totalProfitDollars = Position.Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar) + 
		                                Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
		
		    // Convert the total profit/loss from dollars to ticks
		    // Total profit in ticks = Profit in dollars / (Tick size * Point value per tick)
		    double totalProfitTicks = totalProfitDollars / (this.Instrument.MasterInstrument.TickSize * this.Instrument.MasterInstrument.PointValue);
		
		    // Check if the total profit in ticks exceeds the take profit setting or falls below the stop loss setting
		    // Since TakeProfit1 and StopLoss1 are likely set in ticks, compare directly
		    if (totalProfitTicks >= DailyProfitTarget)
		    {
		        tradingAllowed = false;
		        // Optionally, log or handle the event when trading is halted
		        Log($"Trading halted due to reaching profit or loss limits. Total profit/loss in ticks: {totalProfitTicks}.", LogLevel.Information);
				Print("Trading halted due to reaching profit or loss limits. Total profit/loss in ticks: " + totalProfitTicks);
		    }
		}
		
		private void DrawTrailingLine(string lineName, double priceLevel, Brush color)
		{
		    // Remove previous line to update it to a new position
		    RemoveDrawObject(lineName);
		    Draw.Line(this, lineName, true, 0, priceLevel, -20, priceLevel, color, DashStyleHelper.Solid, 2);
		}

		private DateTime lastProfitTargetHit = DateTime.MinValue;
		private DateTime lastStopLoss1Hit = DateTime.MinValue;
		
		private void HandleTradeExecution(bool targetHit, bool stopLossHit)
		{
		    if (targetHit)
		    {
		        lastProfitTargetHit = DateTime.Now;
		        Log("Profit target hit. Time recorded.", LogLevel.Information);
		    }
		    if (stopLossHit)
		    {
		        lastStopLoss1Hit = DateTime.Now;
		        Log("Stop loss hit. Time recorded.", LogLevel.Information);
		    }
		}

		
		private bool IsCoolingDown()
		{
		    // Only start cooling down when all positions are closed
		    if (Position.Quantity == 0)
		    {
		        DateTime now = DateTime.Now;
		        double secondsSinceLastProfit = (now - lastProfitTargetHit).TotalSeconds;
		        double secondsSinceLastStop = (now - lastStopLoss1Hit).TotalSeconds;
		        bool isCooling = secondsSinceLastProfit < TargetCoolDown || secondsSinceLastStop < StopCoolDown;
		
		        if (isCooling)
		        {
		            Log("Cooling down. Seconds since last profit: " + secondsSinceLastProfit + ", seconds since last stop: " + secondsSinceLastStop, LogLevel.Information);
		            Print("Cooling down. Check logs for details.");
		        }
		
		        return isCooling;
		    }
		    return false;
		}



		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Qty1", Order=0, GroupName="Parameters")]
		public int Qty1
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Qty2", Order=1, GroupName="Parameters")]
		public int Qty2
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="TakeProfit1", Order=2, GroupName="Parameters")]
		public int TakeProfit1
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="StopLoss1", Order=3, GroupName="Parameters")]
		public int StopLoss1
		{ get; set; }
		
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="TakeProfit2", Order=4, GroupName="Parameters")]
		public int TakeProfit2
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="StopLoss2", Order=5, GroupName="Parameters")]
		public int StopLoss2
		{ get; set; }
	
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="TargetCoolDown", Order=8, GroupName="Parameters")]
		public int TargetCoolDown
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="StopCoolDown", Order=9, GroupName="Parameters")]
		public int StopCoolDown
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="DailyProfitTarget", Order=10, GroupName="Parameters")]
		public int DailyProfitTarget
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Start Time", Order=12, GroupName="Trading Hours")]
		public TimeSpan StartTime { get; set; } = new TimeSpan(7, 00, 0);
		
		[NinjaScriptProperty]
		[Display(Name="End Time", Order=13, GroupName="Trading Hours")]
		public TimeSpan EndTime { get; set; } = new TimeSpan(16, 15, 0);
		
		[NinjaScriptProperty]
        [Display(Name="Use Trailing Profit2", Order=14, GroupName="Parameters")]
        public bool UseTrailingProfit2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Trailing Profit2 Ticks", Order=15, GroupName="Parameters")]
        public int TrailingProfit2Ticks
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Trailing Stop Distance Ticks", Order=16, GroupName="Parameters")]
        public int TrailingStopDistanceTicks
        { get; set; }
		
		#endregion
	}
}