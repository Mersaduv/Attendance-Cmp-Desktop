using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AttandenceDesktop.Models
{
    // Helper class for creating COM instance
    public static class ZkemkeeperFactory
    {
        // Creates and returns an instance of the zkemkeeper.CZKEM COM object
        public static dynamic CreateZKEM()
        {
            try
            {
                Program.LogMessage("🔄 Attempting to create ZKTeco SDK instance...");
                
                // فراخوانی مستقیم COM object با CLSID مشخص برای دستگاه‌های ZKTeco
                try {
                    Type type = Type.GetTypeFromCLSID(new Guid("00853A19-BD51-419B-9269-2DABE57EB61F"));
                    if (type != null)
                    {
                        Program.LogMessage("✅ Found COM object via CLSID");
                        dynamic instance = Activator.CreateInstance(type);
                        if (instance != null)
                        {
                            Program.LogMessage("✅ Successfully created COM object instance via CLSID");
                            return instance;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"⚠️ CLSID attempt failed: {ex.Message}");
                }
                
                // روش دوم: استفاده از ProgID
                try {
                    Type type = Type.GetTypeFromProgID("zkemkeeper.CZKEM");
                    if (type != null)
                    {
                        Program.LogMessage("✅ Found COM object via ProgID");
                        dynamic instance = Activator.CreateInstance(type);
                        if (instance != null)
                        {
                            Program.LogMessage("✅ Successfully created COM object instance via ProgID");
                            return instance;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"⚠️ ProgID attempt failed: {ex.Message}");
                }
                
                // اگر COM کار نکرد، تلاش برای رجیستر کردن DLL
                try
                {
                    string[] possibleDllPaths = {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SDK", "zkemkeeper.dll"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Debug", "net9.0", "SDK", "zkemkeeper.dll"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Debug", "net9.0", "zkemkeeper.dll"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", "x86", "zkemkeeper.dll"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zkemkeeper.dll")
                    };
                    
                    foreach (string dllPath in possibleDllPaths)
                    {
                        if (File.Exists(dllPath))
                        {
                            Program.LogMessage($"✅ Found SDK DLL at: {dllPath}");
                            // رجیستر کردن DLL به صورت خودکار
                            RegisterDll(dllPath);
                            
                            // تلاش مجدد برای استفاده از COM
                            try {
                                Type typeAfterReg = Type.GetTypeFromProgID("zkemkeeper.CZKEM");
                                if (typeAfterReg != null)
                                {
                                    dynamic instanceAfterReg = Activator.CreateInstance(typeAfterReg);
                                    if (instanceAfterReg != null)
                                    {
                                        Program.LogMessage("✅ Successfully created COM object after registration");
                                        return instanceAfterReg;
                                    }
                                }
                            }
                            catch { }
                            
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"⚠️ DLL registration failed: {ex.Message}");
                }
                
                // در صورت خطا: ارائه راهنمایی دقیق برای نصب SDK
                Program.LogMessage("❌ Could not create ZKTeco SDK instance. Please follow these steps:");
                Program.LogMessage("1. Make sure zkemkeeper.dll is registered using: regsvr32 /i <path-to-dll>");
                Program.LogMessage("2. Use 32-bit regsvr32 on 64-bit Windows (C:\\Windows\\SysWOW64\\regsvr32.exe)");
                Program.LogMessage("3. Run the application as Administrator");
                Program.LogMessage("4. Make sure all DLL files are copied to the output directory");
                
                throw new Exception("Failed to initialize ZKTeco SDK. Ensure the SDK is properly installed and registered.");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"❌ ERROR in CreateZKEM: {ex.Message}");
                throw;
            }
        }
        
        // Helper method to register a DLL using regsvr32
        private static void RegisterDll(string dllPath)
        {
            try
            {
                Program.LogMessage($"🔄 Attempting to register DLL: {dllPath}");
                
                // Use SysWOW64 regsvr32 for 32-bit DLLs on 64-bit Windows
                string regsvr32Path;
                if (Environment.Is64BitOperatingSystem)
                {
                    // If 64-bit OS, we need to use the 32-bit regsvr32 for our 32-bit DLL
                    regsvr32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "regsvr32.exe");
                    Program.LogMessage("✅ Using 32-bit regsvr32 on 64-bit Windows");
                }
                else
                {
                    // For 32-bit OS, use regular regsvr32
                    regsvr32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "regsvr32.exe");
                    Program.LogMessage("✅ Using regular regsvr32 on 32-bit Windows");
                }
                
                Program.LogMessage($"🔄 Registration path: {regsvr32Path}");
                
                using (System.Diagnostics.Process process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = regsvr32Path;
                    process.StartInfo.Arguments = $"/s \"{dllPath}\"";
                    process.StartInfo.UseShellExecute = true; // Required for elevation
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.Verb = "runas"; // Request admin privileges
                    process.Start();
                    process.WaitForExit();
                    
                    int exitCode = process.ExitCode;
                    if (exitCode == 0)
                    {
                        Program.LogMessage("✅ DLL registered successfully");
                    }
                    else
                    {
                        Program.LogMessage($"⚠️ DLL registration failed with exit code {exitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"⚠️ Error during DLL registration: {ex.Message}");
                // Don't throw here - we'll try other methods if registration fails
            }
        }
    }

    // ZKTeco COM interface
    [ComImport, Guid("1A18F0D4-1DEB-4941-A8D2-454CAD4594EE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IZKEM
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(3)]
        bool Connect_Net(string IPAdd, int Port);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(4)]
        bool Disconnect();
        
        [DispId(5)]
        int GetLastError();
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(6)]
        bool RegEvent(int machineNumber, int eventMask);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(7)]
        bool EnableDevice(int machineNumber, bool enabled);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(8)]
        bool RefreshData(int machineNumber);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(9)]
        bool ReadAllUserID(int machineNumber);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(10)]
        bool ReadAllTemplate(int machineNumber);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(11)]
        bool SSR_GetAllUserInfo(
            int machineNumber, 
            [MarshalAs(UnmanagedType.BStr)] out string enrollNo, 
            [MarshalAs(UnmanagedType.BStr)] out string name, 
            [MarshalAs(UnmanagedType.BStr)] out string password, 
            out int privilege, 
            [MarshalAs(UnmanagedType.Bool)] out bool enabled);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(12)]
        bool SSR_GetUserTmpStr(
            int machineNumber, 
            [MarshalAs(UnmanagedType.BStr)] string enrollNumber, 
            int fingerIndex, 
            [MarshalAs(UnmanagedType.BStr)] out string fingerData, 
            out int size, 
            int flag);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(13)]
        bool ReadGeneralLogData(int machineNumber);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(14)]
        bool SSR_GetGeneralLogData(
            int machineNumber, 
            [MarshalAs(UnmanagedType.BStr)] out string enrollNumber, 
            out int verifyMode, 
            out int inOutMode, 
            out int year, 
            out int month, 
            out int day, 
            out int hour, 
            out int minute, 
            out int second, 
            ref int workcode);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(15)]
        bool SetCommPassword(int commKey);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(16)]
        bool GetDeviceInfo(int machineNumber, int infoType, ref int value);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(17)]
        bool GetProductCode(int machineNumber, [MarshalAs(UnmanagedType.BStr)] ref string productCode);
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DispId(18)]
        bool GetFirmwareVersion(int machineNumber, [MarshalAs(UnmanagedType.BStr)] ref string version);
    }
} 