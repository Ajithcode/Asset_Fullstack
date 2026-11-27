using WorkerNetworkService.Services;

namespace WorkerNetworkService.Settings;

/// <summary>
/// Fast zone resolver (section Location:Fast). It reads the NetworkServer fast lane and decides per tag in seconds.
/// Shadow = true: decisions of every variant go to the shadow log only (no TagLocationState / ZoneVisit / alert writes).
/// Location:FastMode (or the Redis flag) = true: the primary variant (Params) writes TagLocationState, ZoneVisit, the
/// zone_in / zone_out compatibility documents and pushes changes over Redis pub/sub.
/// </summary>
public class FastLocationSettings
{
    public bool Shadow { get; set; }

    /// <summary>While the fast mode is live: keep running the 15 s resolver log-only (variant slow15 in the shadow log) for a reverse comparison.</summary>
    public bool ShadowSlow { get; set; }
    public string Stream { get; set; } = "networkserver_fast";
    public string Group { get; set; } = "fast-resolver";
    public string FlagKey { get; set; } = "a360:flags:location_fast_mode";
    public string PubSubChannel { get; set; } = "a360:tag_location";
    public string ShadowLogCollection { get; set; } = "LocationShadowLog";
    public string LatencyCollection { get; set; } = "LocationLatency";
    public string VisitCollection { get; set; } = "ZoneVisit";
    public int LogRetentionDays { get; set; } = 7;
    public int LastSeenThrottleSeconds { get; set; } = 5;
    public int EvalIntervalMs { get; set; } = 1000;
    public int SnapshotSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 200;

    /// <summary>The primary variant: the one that goes live.</summary>
    public FastParams Params { get; set; } = new() { Name = "primary" };

    /// <summary>Extra variants that only run in shadow mode: overrides on top of Params.</summary>
    public List<FastVariant> Variants { get; set; } = [];
}

public class FastVariant
{
    public string Name { get; set; } = "";
    public double? TauSeconds { get; set; }
    public double? LeadMarginDb { get; set; }
    public double? MinDwellSeconds { get; set; }
    public double? StaleSecondsGateway { get; set; }
    public double? StaleSecondsRepeater { get; set; }

    public FastParams Apply(FastParams baseParams)
    {
        var p = baseParams.Clone();
        p.Name = Name;
        if (TauSeconds is { } a) p.TauSeconds = a;
        if (LeadMarginDb is { } b) p.LeadMarginDb = b;
        if (MinDwellSeconds is { } c) p.MinDwellSeconds = c;
        if (StaleSecondsGateway is { } d) p.StaleSecondsGateway = d;
        if (StaleSecondsRepeater is { } e) p.StaleSecondsRepeater = e;
        return p;
    }
}
