# Updating Official Source Code

The `System.Device.Gpio` implementation in this repository acts as a shim that perfectly mimics the official Microsoft library, while intercepting driver creation to use our local simulators. 

To prevent behavioral drift, we periodically pull the core struct and class files directly from the [dotnet/iot repository](https://github.com/dotnet/iot).

## Using the Sync Script

We provide a shell script to automate the process of downloading the official source and comparing it with our local implementation.

1. **Run the script** from the root of the repository:
   ```bash
   ./scripts/sync-official-source.sh
   ```

2. **Review the differences**:
   The script will download the latest files from the official `main` branch to a temporary directory and output a `diff` comparing them against our `src/System.Device.Gpio/` folder.
   
   Look closely at the differences. Some files (like `GpioController.cs`) will intentionally have differences because our shim intercepts driver creation to inject simulators.

3. **Apply updates**:
   If you want to automatically apply the official files to our repository's tracked files, run the script with the `--update` (or `-u`) flag:
   ```bash
   ./scripts/sync-official-source.sh --update
   ```
   This will safely copy only the matching tracked files and automatically clean up the temporary directory afterwards.

4. **Re-apply Shim Adjustments**:
   If you overwrite `GpioController.cs` or `GpioDriver.cs`, you may need to re-apply our custom logic (for example, the custom `OpenPin` implementation that ensures pin tracking without pulling in full OS-level hardware driver discovery).

5. **Run Tests**:
   After updating the source, run the test suite to ensure binary and behavioral compatibility:
   ```bash
   dotnet test
   ```
   Both the `DevDecoder.GpioSimulator.Tests` (testing against our shim) and `DevDecoder.GpioSimulator.OfficialTests` (testing against the official NuGet package) must pass.
