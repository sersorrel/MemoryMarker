using System;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using KamiLib.Utilities;

namespace MemoryMarker.Controllers;

public class MemoryMarkerSystem : IDisposable
{
    public static Configuration Configuration { get; private set; } = null!;
    public static AddonFieldMarkerContextMenu ContextMenu { get; private set; } = null!;
    public static AddonFieldMarkerController FieldMarkerController { get; private set; } = null!;

    public MemoryMarkerSystem()
    {
        Configuration = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ContextMenu = new AddonFieldMarkerContextMenu();
        FieldMarkerController = new AddonFieldMarkerController();
        
        if (Condition.IsBoundByDuty()) OnZoneChange(Service.ClientState.TerritoryType);
        
        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    private void OnZoneChange(ushort territoryType)
    {
        if (Service.ClientState.IsPvP) return;

        if (Service.PluginInterface.InstalledPlugins.Any(pluginInfo => pluginInfo is { InternalName: "WaymarkPresetPlugin", IsLoaded: true }))
        {
            Service.Log.Information("WaymarkPreset plugin detected, skipping writing waymarks to memory");
        }
        
        // If we are bound by duty after changing zones, we need to either generate new markers data, or load existing.
        else if (Condition.IsBoundByDuty())
        {
            if (Configuration.FieldMarkerData.TryAdd(territoryType, new ZoneMarkerData()))
            {
                SetZoneMarkerData(markers);
                Service.Log.Debug($"[Territory: {territoryType}] Loading Waymarks, Count: {markers.MarkerData.OfType<NamedMarker>().Count()}");
                Service.Log.Debug($"No markers for {territoryType}, creating");
                Configuration.Save();
            }

            var markersForTerritory = Configuration.FieldMarkerData[territoryType];

            Service.Log.Info($"[Territory: {territoryType, 4}] Loading Waymarks, Count: {markersForTerritory.Count}");
            SetZoneMarkerData(markersForTerritory);
        }
    }

    private static unsafe void SetZoneMarkerData(ZoneMarkerData data)
    {
        foreach (var index in Enumerable.Range(0, data.MarkerData.Length))
        {
            var savedMarker = data.MarkerData[index]?.Marker;
            var targetAddress = FieldMarkerModule.Instance()->PresetArraySpan.Get(index);

            if (savedMarker is null)
            {
                Marshal.Copy(new byte[sizeof(FieldMarkerPreset)], 0, (nint) targetAddress, sizeof(FieldMarkerPreset));
            }
            else
            {
                Marshal.StructureToPtr(savedMarker, (nint) targetAddress, false);
            }
        }
    }
    
    public void Dispose()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        
        ContextMenu.Dispose();
        FieldMarkerController.Dispose();
    }
}