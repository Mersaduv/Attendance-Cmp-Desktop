using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;

namespace AttandenceDesktop.Services;

/// <summary>
/// Service for connecting to ZKTeco attendance devices over TCP/IP (via the classic zkemkeeper COM SDK)
/// and retrieving raw punch logs.  This implementation is intentionally light-weight so it can be
/// invoked from view-models to merely test connectivity or fetch data on-demand.
/// </summary>
public sealed class ZkTecoConnectionService : IDisposable
{
    private readonly dynamic _zk;
    private bool _connected;

    public ZkTecoConnectionService()
    {
        // Try to create COM instance of the classic ZKEM SDK.
        // The SDK must be installed and "zkemkeeper.dll" registered with regsvr32.
        var comType = Type.GetTypeFromProgID("zkemkeeper.CZKEM");
        if (comType == null)
            throw new InvalidOperationException("ZKTeco SDK is not installed or zkemkeeper.dll is not registered. Install the SDK and run: regsvr32 zkemkeeper.dll");

        _zk = Activator.CreateInstance(comType) ?? throw new InvalidOperationException("Failed to create CZKEM instance (COM)");
    }

    /// <summary>
    /// Attempts to establish a TCP connection to the device. Returns <c>true</c> on success.
    /// </summary>
    public bool Connect(Device device)
    {
        if (_connected) return true;
        if (device == null) throw new ArgumentNullException(nameof(device));

        // Connect_Net returns true when connection succeeds.
        _connected = _zk.Connect_Net(device.IPAddress, device.Port);
        return _connected;
    }

    /// <summary>
    /// Reads all logs currently stored in the device (optionally filtering by date).
    /// NOTE: employee mapping by ZkUserId must be done by the caller (EnrollNumber → Employee).
    /// </summary>
    /// <param name="device">Device entity record.</param>
    /// <param name="since">If provided, only logs newer than this timestamp are returned.</param>
    public List<PunchLog> FetchLogs(Device device, DateTime? since = null)
    {
        if (!_connected) Connect(device);
        var logs = new List<PunchLog>();

        // Ensure the device will push log data into the internal buffer.
        // ReadGeneralLogData loads the entire log into SDK memory.
        bool prepared = _zk.ReadGeneralLogData(device.MachineNumber);
        if (!prepared) return logs; // no data or error

        // Fields used by SSR_GetGeneralLogData
        string enrollNumber;
        int verifyMode, inOutMode, year, month, day, hour, minute, second, workCode = 0;

        while (true)
        {
            /* bool result = _zk.SSR_GetGeneralLogData(machine, out enrollNumber, out verifyMode, out inOutMode,
                                                       out year, out month, out day, out hour, out minute, out second);
             * Unfortunately the COM interop signature generated at runtime with 'dynamic' does not support
             * ref/out for the last parameter (workCode).  To keep compilation simple we rely on reflection
             * through dynamic invocation which still works fine – just ensure the variable list matches.
             */
            bool hasData = _zk.SSR_GetGeneralLogData(device.MachineNumber,
                                                     out enrollNumber,
                                                     out verifyMode,
                                                     out inOutMode,
                                                     out year,
                                                     out month,
                                                     out day,
                                                     out hour,
                                                     out minute,
                                                     out second,
                                                     ref workCode);
            if (!hasData) break;

            var punchTime = new DateTime(year, month, day, hour, minute, second);
            if (since.HasValue && punchTime <= since.Value) continue;

            // Map punch type (this may vary between devices/firmware – adjust as needed)
            PunchType pType = inOutMode switch
            {
                0 => PunchType.CheckIn,
                1 => PunchType.CheckOut,
                _ => PunchType.Unknown
            };

            logs.Add(new PunchLog
            {
                DeviceId = device.Id,
                Device = device,
                EmployeeId = 0, // will be resolved later using enrollNumber/ZkUserId
                PunchTime = punchTime,
                PunchType = pType,
                DeviceRowId = $"{enrollNumber}-{punchTime:yyyyMMddHHmmss}"
            });
        }

        return logs;
    }

    public void Dispose()
    {
        if (_connected)
        {
            try { _zk.Disconnect(); } catch { /* ignored */ }
            _connected = false;
        }
    }
} 