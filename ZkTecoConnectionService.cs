using System;

public class ZkTecoConnectionService
{
    public void Connect()
    {
        var comType = Type.GetTypeFromProgID("zkemkeeper.CZKEM");
        if (comType == null)
        {
            var bitnessMsg = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            throw new InvalidOperationException($"ZKTeco SDK is not installed or zkemkeeper.dll is not registered for {bitnessMsg} applications. Install the proper SDK version and register the DLL using regsvr32. TIP: On a 64-bit Windows register x64 DLL with %SystemRoot%\\System32\\regsvr32.exe and x86 DLL with %SystemRoot%\\SysWOW64\\regsvr32.exe.");
        }
    }
} 