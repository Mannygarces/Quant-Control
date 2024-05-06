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
