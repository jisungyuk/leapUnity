using System;
using System.IO.Ports;
using System.Linq;
using Microsoft.Win32;
using UnityEngine;

/// <summary>
/// Finds the COM port of the TTL TriggerBox by its device name, so the same build works
/// on PCs where Windows assigned it a different port number (COM3 on one lab PC, COM5 on
/// another). Device names come from the registry (HKLM\SYSTEM\CurrentControlSet\Enum),
/// which keeps entries for unplugged devices too, so only ports Windows currently reports
/// as present are considered.
/// </summary>
public static class TtlPortFinder
{
    const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum";

    /// <summary>
    /// Returns the port to open: the present port whose device name contains deviceNameMatch,
    /// otherwise fallbackPort if it is present, otherwise null. foundByName tells which case applied.
    /// </summary>
    public static string Resolve(string deviceNameMatch, string fallbackPort, out bool foundByName)
    {
        foundByName = false;

        string[] present;
        try { present = SerialPort.GetPortNames(); }
        catch (Exception e)
        {
            Debug.LogWarning($"[TtlPortFinder] Could not list COM ports: {e.Message}");
            return fallbackPort;
        }

        string found = FindByDeviceName(deviceNameMatch, present);
        if (found != null)
        {
            Debug.Log($"[TtlPortFinder] Found '{deviceNameMatch}' on {found}.");
            foundByName = true;
            return found;
        }

        string available = present.Length > 0 ? string.Join(", ", present) : "none";
        if (!string.IsNullOrEmpty(fallbackPort) && present.Contains(fallbackPort, StringComparer.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[TtlPortFinder] No '{deviceNameMatch}' device found — using configured {fallbackPort} (available: {available}).");
            return fallbackPort;
        }

        Debug.LogWarning($"[TtlPortFinder] No '{deviceNameMatch}' device found and configured port '{fallbackPort}' is not present (available: {available}).");
        return null;
    }

    // Walks Enum\<bus>\<device>\<instance> looking for a "Device Parameters\PortName" value
    // on an instance whose FriendlyName contains the match string.
    static string FindByDeviceName(string deviceNameMatch, string[] present)
    {
        if (string.IsNullOrEmpty(deviceNameMatch)) return null;

        using RegistryKey root = TryOpen(Registry.LocalMachine, EnumRoot);
        if (root == null)
        {
            Debug.LogWarning($"[TtlPortFinder] Could not open HKLM\\{EnumRoot}.");
            return null;
        }

        foreach (string bus in root.GetSubKeyNames())
        {
            using RegistryKey busKey = TryOpen(root, bus);
            if (busKey == null) continue;

            foreach (string device in busKey.GetSubKeyNames())
            {
                using RegistryKey deviceKey = TryOpen(busKey, device);
                if (deviceKey == null) continue;

                foreach (string instance in deviceKey.GetSubKeyNames())
                {
                    using RegistryKey instanceKey = TryOpen(deviceKey, instance);
                    if (instanceKey == null) continue;

                    string friendlyName = instanceKey.GetValue("FriendlyName") as string;
                    if (friendlyName == null || friendlyName.IndexOf(deviceNameMatch, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    using RegistryKey paramsKey = TryOpen(instanceKey, "Device Parameters");
                    string portName = paramsKey?.GetValue("PortName") as string;
                    if (portName != null && present.Contains(portName, StringComparer.OrdinalIgnoreCase))
                        return portName;
                }
            }
        }
        return null;
    }

    // Some Enum subkeys are ACL-protected for non-admin users; skip those instead of
    // aborting the whole search.
    static RegistryKey TryOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name); }
        catch (Exception) { return null; }
    }
}
