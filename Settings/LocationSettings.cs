namespace WorkerNetworkService.Settings;

/// <summary>
/// Zone resolution per tag (see TagLocationResolver). ResolverEnabled=false switches back to the old per-gateway
/// DeviceTriggerTracker (config change + restart, no rebuild).
/// </summary>
public class LocationSettings
{
    public bool ResolverEnabled { get; set; } = true;

    /// <summary>Median over the last N windows.</summary>
    public int RssiWindowCount { get; set; } = 3;

    /// <summary>A device needs samples in at least this many of those windows to compete.</summary>
    public int RssiMinWindows { get; set; } = 2;

    /// <summary>A new device must be this many dB stronger (median) than the current one ...</summary>
    public double SwitchMarginDb { get; set; } = 6;

    /// <summary>... for this many consecutive windows before the tag switches.</summary>
    public int SwitchConsecutiveWindows { get; set; } = 1;

    /// <summary>The current device has had no sample for this long and a challenger qualifies: switch at once.</summary>
    public int CurrentDeviceSilenceSeconds { get; set; } = 30;

    /// <summary>A device heard in the latest window this many dB stronger than the current device's latest sample: switch after one window.</summary>
    public double FastSwitchMarginDb { get; set; } = 15;

    /// <summary>Length of one NetworkServer aggregation window.</summary>
    public int WindowSeconds { get; set; } = 15;

    /// <summary>Mongo collection with the resolved state (read by tracking, dashboard, asset).</summary>
    public string StateCollection { get; set; } = "TagLocationState";

    /// <summary>Seconds between reloads of the plotted devices (maps_plots).</summary>
    public int PlotCacheSeconds { get; set; } = 30;
}
