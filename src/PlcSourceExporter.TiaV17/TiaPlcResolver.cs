using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;

namespace PlcSourceExporter.TiaV17;

public sealed class PlcCandidate
{
    public PlcCandidate(string displayName, Device device, DeviceItem deviceItem, PlcSoftware plcSoftware)
    {
        DisplayName = displayName;
        Device = device;
        DeviceItem = deviceItem;
        PlcSoftware = plcSoftware;
    }

    public string DisplayName { get; }

    public Device Device { get; }

    public DeviceItem DeviceItem { get; }

    public PlcSoftware PlcSoftware { get; }
}

public static class TiaPlcResolver
{
    public static PlcSoftware? TryResolvePlcSoftware(DeviceItem deviceItem)
    {
        if (deviceItem == null)
        {
            return null;
        }

        var softwareContainer = deviceItem.GetService<SoftwareContainer>();
        return softwareContainer?.Software as PlcSoftware;
    }

    public static IReadOnlyList<PlcCandidate> FindPlcs(Project project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var result = new List<PlcCandidate>();
        foreach (var device in project.Devices)
        {
            foreach (var deviceItem in EnumerateDeviceItems(device.DeviceItems))
            {
                var plcSoftware = TryResolvePlcSoftware(deviceItem);
                if (plcSoftware != null)
                {
                    result.Add(new PlcCandidate($"{device.Name}/{deviceItem.Name}", device, deviceItem, plcSoftware));
                }
            }
        }

        return result;
    }

    public static PlcCandidate SelectPlc(Project project, string? plcName)
    {
        var candidates = FindPlcs(project);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No PLC software was found in the open project.");
        }

        if (string.IsNullOrWhiteSpace(plcName))
        {
            return candidates[0];
        }

        var matches = candidates
            .Where(candidate =>
                string.Equals(candidate.Device.Name, plcName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DeviceItem.Name, plcName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, plcName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"PLC name '{plcName}' matched more than one PLC. Use the full '<device>/<device item>' name.");
        }

        var available = string.Join(", ", candidates.Select(candidate => candidate.DisplayName));
        throw new InvalidOperationException($"PLC name '{plcName}' was not found. Available PLCs: {available}");
    }

    private static IEnumerable<DeviceItem> EnumerateDeviceItems(DeviceItemComposition deviceItems)
    {
        foreach (var deviceItem in deviceItems)
        {
            yield return deviceItem;

            foreach (var child in EnumerateDeviceItems(deviceItem.DeviceItems))
            {
                yield return child;
            }
        }
    }
}
