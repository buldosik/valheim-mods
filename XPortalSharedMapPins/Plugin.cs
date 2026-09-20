using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace XPortalSharedMapPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("vapok.mods.xportalnetworks", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "buldosik.XPortalSharedMapPins";
        public const string PluginName = "XPortal Shared Map Pins";
        public const string PluginVersion = "0.2.0";

        private const string PinPrefix = "";
        private static ManualLogSource Log = null!;
        private static Plugin? Instance;
        private static readonly Dictionary<string, Vector3> ManagedPins = new Dictionary<string, Vector3>();

        private ConfigEntry<float> _refreshInterval = null!;
        private ConfigEntry<bool> _includePrivatePortals = null!;
        private ConfigEntry<bool> _showPortalPins = null!;
        private ConfigEntry<bool> _showNetworkInPinName = null!;
        private ConfigEntry<bool> _restoreXPortalPins = null!;
        private ConfigEntry<Color> _pinColor = null!;
        private float _nextRefresh;
        private bool _warnedAboutApi;
        private MethodInfo? _getList;
        private MethodInfo? _getPortalList;
        private PropertyInfo? _managerInstance;
        private MethodInfo? _addPin;
        private MethodInfo? _removePin;
        private FieldInfo? _pinsField;
        private FieldInfo? _pinNameField;
        private FieldInfo? _pinPositionField;
        private FieldInfo? _pinTypeField;
        private FieldInfo? _pinIconField;
        private FieldInfo? _pinIconElementField;
        private FieldInfo? _iconsField;
        private FieldInfo? _visibleIconTypesField;
        private FieldInfo? _spriteDataNameField;
        private FieldInfo? _spriteDataIconField;
        private object? _portalManager;
        private object? _customPinType;
        private Sprite? _portalIcon;
        private bool _customPinTypeInstalled;
        private Harmony? _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _refreshInterval = Config.Bind("General", "RefreshIntervalSeconds", 5f,
                "How often the local portal list is reflected onto the map.");
            _includePrivatePortals = Config.Bind("General", "IncludePrivatePortals", false,
                "Also add pins for private and personal-network portals. Each player sees these locally.");
            _showPortalPins = Config.Bind("General", "ShowPortalPins", true,
                "Show XPortalNetworks portals on the map. Disable to remove only this mod's portal pins.");
            _showNetworkInPinName = Config.Bind("General", "ShowNetworkInPinName", false,
                "Show the XPortalNetworks network name in generated map pin names.");
            _pinColor = Config.Bind("General", "PinColor", new Color(0.4f, 0.8f, 1f, 1f),
                "Color of the generated portal pin icon. Configuration Manager provides a color picker.");
            _restoreXPortalPins = Config.Bind("Actions", "RestoreXPortalPins", false,
                "Set to true to recreate only XPortal-generated pins; vanilla player pins are not touched.");

            CacheXPortalApi();
            CacheMapApi();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("Loaded. Portal pins are client-local and synchronized from XPortalNetworks.");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh || Minimap.instance == null)
                return;

            _nextRefresh = Time.unscaledTime + Mathf.Max(1f, _refreshInterval.Value);
            EnsureCustomPortalType();
            if (!_showPortalPins.Value)
            {
                RemoveAllManagedPins();
                ManagedPins.Clear();
                return;
            }
            if (_restoreXPortalPins.Value)
            {
                RestoreAllPins();
                _restoreXPortalPins.Value = false;
                Config.Save();
            }

            RefreshPins();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (ReferenceEquals(Instance, this))
                Instance = null;
            RemoveAllManagedPins();
            ManagedPins.Clear();
        }

        private void CacheXPortalApi()
        {
            try
            {
                Type? managerType = Type.GetType("XPortalNetworks.KnownPortalsManager, XPortalNetworks");
                if (managerType == null)
                    throw new InvalidOperationException("XPortalNetworks.KnownPortalsManager was not found");

                _managerInstance = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _getList = managerType.GetMethod("GetList", BindingFlags.Public | BindingFlags.Instance);
                _portalManager = _managerInstance?.GetValue(null, null);
                _getPortalList = typeof(ZDOMan).GetMethod("GetPortalList", BindingFlags.Public | BindingFlags.Instance);

                if (_managerInstance == null || _getList == null || _portalManager == null)
                    throw new MissingMethodException("XPortalNetworks portal list API is unavailable");
            }
            catch (Exception ex)
            {
                Log.LogError($"Could not connect to XPortalNetworks: {ex.GetBaseException().Message}");
            }
        }

        private void CacheMapApi()
        {
            try
            {
                Type mapType = typeof(Minimap);
                _addPin = mapType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "AddPin" && IsAddPinSignature(method));
                _removePin = mapType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "RemovePin" && IsRemovePinSignature(method));
                Type? pinDataType = mapType.GetNestedType("PinData", BindingFlags.Public | BindingFlags.NonPublic);
                _pinsField = mapType.GetField("m_pins", BindingFlags.NonPublic | BindingFlags.Instance);
                const BindingFlags instanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _pinNameField = pinDataType?.GetField("m_name", instanceFields);
                _pinPositionField = pinDataType?.GetField("m_pos", instanceFields);
                _pinTypeField = pinDataType?.GetField("m_type", instanceFields);
                _pinIconField = pinDataType?.GetField("m_icon", instanceFields);
                _pinIconElementField = pinDataType?.GetField("m_iconElement", instanceFields);
                _iconsField = mapType.GetField("m_icons", instanceFields);
                _visibleIconTypesField = mapType.GetField("m_visibleIconTypes", BindingFlags.NonPublic | BindingFlags.Instance);
                Type? spriteDataType = mapType.GetNestedType("SpriteData", BindingFlags.Public | BindingFlags.NonPublic);
                _spriteDataNameField = spriteDataType?.GetField("m_name", instanceFields);
                _spriteDataIconField = spriteDataType?.GetField("m_icon", instanceFields);
                if (_addPin == null || _removePin == null || _pinsField == null || _pinNameField == null ||
                    _pinPositionField == null || _pinTypeField == null ||
                    _pinIconField == null || _iconsField == null || _visibleIconTypesField == null ||
                    _spriteDataNameField == null || _spriteDataIconField == null)
                    throw new MissingMethodException("Valheim Minimap pin API is unavailable");
            }
            catch (Exception ex)
            {
                Log.LogError($"Could not connect to Valheim map pin API: {ex.GetBaseException().Message}");
            }
        }

        private static bool IsAddPinSignature(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length >= 5 && parameters.Length <= 7 &&
                   parameters[0].ParameterType == typeof(Vector3) &&
                   parameters[1].ParameterType.IsEnum &&
                   parameters[2].ParameterType == typeof(string) &&
                   parameters[3].ParameterType == typeof(bool) &&
                   parameters[4].ParameterType == typeof(bool) &&
                   (parameters.Length == 5 || parameters.Length == 6 || parameters.Length == 7);
        }

        private static bool IsRemovePinSignature(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
                 Type? pinDataType = typeof(Minimap).GetNestedType("PinData", BindingFlags.Public | BindingFlags.NonPublic);
                 return parameters.Length == 1 && pinDataType != null && parameters[0].ParameterType == pinDataType;
        }

        private void RefreshPins()
        {
            if (_getList == null || _portalManager == null || _addPin == null || _removePin == null)
            {
                WarnOnce(ref _warnedAboutApi, "Portal or map APIs are unavailable; no pins will be created.");
                return;
            }

            try
            {
                var wanted = new Dictionary<string, Vector3>();
                IEnumerable? portals = _getList.Invoke(_portalManager, null) as IEnumerable;
                if (portals != null)
                {
                    foreach (object portal in portals)
                        AddKnownPortalPin(wanted, portal);
                }

                AddLocalPortalZdos(wanted);

                foreach (KeyValuePair<string, Vector3> existing in ManagedPins.ToList())
                {
                    if (!wanted.TryGetValue(existing.Key, out Vector3 newLocation) || newLocation != existing.Value)
                    {
                        RemovePin(existing.Key, existing.Value);
                        ManagedPins.Remove(existing.Key);
                    }
                }

                foreach (KeyValuePair<string, Vector3> desired in wanted)
                {
                    if (ManagedPins.ContainsKey(desired.Key))
                        continue;

                    AddPin(desired.Key, desired.Value);
                    ManagedPins[desired.Key] = desired.Value;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Refreshing portal pins failed: {ex.GetBaseException().Message}");
            }
        }

        private void AddKnownPortalPin(Dictionary<string, Vector3> wanted, object portal)
        {
            if (!TryReadPortal(portal, out string name, out Vector3 location, out long networkOwner,
                    out bool isPrivate, out string networkName))
                return;
            if (!_includePrivatePortals.Value && !IsSharedPortal(networkOwner, isPrivate))
                return;

            AddWantedPin(wanted, name, location, networkName);
        }

        private void AddLocalPortalZdos(Dictionary<string, Vector3> wanted)
        {
            if (_getPortalList == null || ZDOMan.instance == null)
                return;

            IEnumerable? portalZdos = _getPortalList.Invoke(ZDOMan.instance, null) as IEnumerable;
            if (portalZdos == null)
                return;

            foreach (object zdo in portalZdos)
            {
                Vector3 location = ((ZDO)zdo).GetPosition();
                if (wanted.Values.Any(existing => (existing - location).sqrMagnitude < 0.25f))
                    continue;

                string name = ((ZDO)zdo).GetString("tag", string.Empty);
                AddWantedPin(wanted, name, location, "Global");
            }
        }

        private void AddWantedPin(Dictionary<string, Vector3> wanted, string name, Vector3 location, string networkName)
        {
            string portalName = string.IsNullOrWhiteSpace(name) ? "Unnamed portal" : name.Trim();
            string networkPrefix = _showNetworkInPinName.Value && !string.IsNullOrWhiteSpace(networkName)
                ? $"[{networkName}] "
                : string.Empty;
            string pinName = PinPrefix + networkPrefix + portalName;
            string uniqueName = pinName;
            int duplicate = 2;
            while (wanted.ContainsKey(uniqueName))
                uniqueName = $"{pinName} ({duplicate++})";
            wanted[uniqueName] = location;
        }

        private static bool IsSharedPortal(long networkOwner, bool isPrivate)
        {
            return !isPrivate && (networkOwner == 0L || (networkOwner >= 1L && networkOwner <= 15L));
        }

        private static bool TryReadPortal(object portal, out string name, out Vector3 location, out long networkOwner,
            out bool isPrivate, out string networkName)
        {
            name = string.Empty;
            location = Vector3.zero;
            networkOwner = 0L;
            isPrivate = false;
            networkName = string.Empty;

            Type type = portal.GetType();
            PropertyInfo? nameProperty = type.GetProperty("Name");
            PropertyInfo? locationProperty = type.GetProperty("Location");
            PropertyInfo? ownerProperty = type.GetProperty("NetworkOwnerPlayerId");
            PropertyInfo? ownerNameProperty = type.GetProperty("NetworkOwnerDisplayName");
            PropertyInfo? privateProperty = type.GetProperty("IsPrivate");
            if (nameProperty == null || locationProperty == null || ownerProperty == null ||
                ownerNameProperty == null || privateProperty == null)
                return false;

            name = nameProperty.GetValue(portal, null) as string ?? string.Empty;
            location = (Vector3)(locationProperty.GetValue(portal, null) ?? Vector3.zero);
            networkOwner = Convert.ToInt64(ownerProperty.GetValue(portal, null));
            isPrivate = Convert.ToBoolean(privateProperty.GetValue(portal, null));
            networkName = ownerNameProperty.GetValue(portal, null) as string ?? string.Empty;
            if (networkOwner == 0L)
                networkName = "Global";
            else if (networkOwner >= 1L && networkOwner <= 15L && string.IsNullOrWhiteSpace(networkName))
                networkName = $"Network {networkOwner}";
            else if (string.IsNullOrWhiteSpace(networkName))
                networkName = "Personal";
            return true;
        }

        private void AddPin(string name, Vector3 location)
        {
            Type pinType = _addPin!.GetParameters()[1].ParameterType;
            object portalPin = _customPinType ?? Enum.Parse(pinType, "Icon3");
            ParameterInfo[] parameters = _addPin.GetParameters();
            var args = new List<object> { location, portalPin, name, false, false };
            if (parameters.Length >= 6)
                args.Add(0L);
            if (parameters.Length == 7)
                args.Add(Activator.CreateInstance(parameters[6].ParameterType)!);

            object? pin = _addPin.Invoke(Minimap.instance, args.ToArray());
            ApplyPortalIcon(pin);
        }

        private void RemovePin(string name, Vector3 location)
        {
            foreach (object pin in GetMapPins().Cast<object>().ToList())
            {
                if (Equals(_pinNameField!.GetValue(pin), name) &&
                    (Vector3)_pinPositionField!.GetValue(pin)! == location)
                {
                    _removePin!.Invoke(Minimap.instance, new[] { pin });
                    return;
                }
            }
        }

        private IEnumerable GetMapPins()
        {
            return (_pinsField?.GetValue(Minimap.instance) as IEnumerable) ?? Array.Empty<object>();
        }

        private void EnsureCustomPortalType()
        {
            if (_customPinTypeInstalled || Minimap.instance == null || _iconsField == null ||
                _visibleIconTypesField == null || _spriteDataNameField == null || _spriteDataIconField == null)
                return;

            IList? icons = _iconsField.GetValue(Minimap.instance) as IList;
            if (icons == null)
                return;

            foreach (object entry in icons)
            {
                Sprite? icon = _spriteDataIconField.GetValue(entry) as Sprite;
                if (icon != null && icon.name.IndexOf("portal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _portalIcon = icon;
                    break;
                }
            }

            _portalIcon ??= CreateFallbackPortalIcon();
            Type pinType = _addPin!.GetParameters()[1].ParameterType;
            int customValue = Enum.GetValues(pinType).Length;
            _customPinType = Enum.ToObject(pinType, customValue);

            Type spriteDataType = _spriteDataNameField.DeclaringType!;
            object spriteData = Activator.CreateInstance(spriteDataType)!;
            _spriteDataNameField.SetValue(spriteData, _customPinType);
            _spriteDataIconField.SetValue(spriteData, _portalIcon);
            icons.Add(spriteData);

            bool[]? visible = _visibleIconTypesField.GetValue(Minimap.instance) as bool[];
            if (visible != null && visible.Length <= customValue)
            {
                bool[] expanded = new bool[customValue + 1];
                for (int i = 0; i < expanded.Length; i++)
                    expanded[i] = i >= visible.Length || visible[i];
                _visibleIconTypesField.SetValue(Minimap.instance, expanded);
            }

            _customPinTypeInstalled = true;
        }

        private Sprite CreateFallbackPortalIcon()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "XPortalSharedMapPins_PortalIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    float alpha = Mathf.Clamp01((0.9f - Mathf.Abs(distance - 9.5f)) * 4f);
                    pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        private void ApplyPortalIcon(object? pin)
        {
            if (pin == null || _portalIcon == null)
                return;

            _pinTypeField?.SetValue(pin, _customPinType);
            _pinIconField?.SetValue(pin, _portalIcon);
            object? iconElement = _pinIconElementField?.GetValue(pin);
            SetImageSprite(iconElement, _portalIcon);
            PropertyInfo? colorProperty = iconElement?.GetType().GetProperty("color");
            colorProperty?.SetValue(iconElement, _pinColor.Value, null);
        }

        private static void SetImageSprite(object? image, Sprite sprite)
        {
            PropertyInfo? spriteProperty = image?.GetType().GetProperty("sprite");
            spriteProperty?.SetValue(image, sprite, null);
        }

        private void RestoreAllPins()
        {
            RemoveAllManagedPins();
            ManagedPins.Clear();
        }

        private void RemoveAllManagedPins()
        {
            if (Minimap.instance == null || _removePin == null)
                return;

            foreach (KeyValuePair<string, Vector3> pin in ManagedPins)
            {
                try
                {
                    RemovePin(pin.Key, pin.Value);
                }
                catch (Exception ex)
                {
                    Log.LogDebug($"Could not remove managed map pin: {ex.GetBaseException().Message}");
                }
            }
        }

        private void ReapplyPortalColors()
        {
            if (_pinTypeField == null || _pinIconElementField == null || _customPinType == null || Minimap.instance == null)
                return;

            foreach (object pin in GetMapPins().Cast<object>())
            {
                if (!Equals(_pinTypeField.GetValue(pin), _customPinType))
                    continue;

                object? iconElement = _pinIconElementField.GetValue(pin);
                PropertyInfo? colorProperty = iconElement?.GetType().GetProperty("color");
                colorProperty?.SetValue(iconElement, _pinColor.Value, null);
            }
        }

        [HarmonyPatch(typeof(Minimap), "UpdatePins")]
        private static class MinimapUpdatePinsPatch
        {
            private static void Postfix()
            {
                Instance?.ReapplyPortalColors();
            }
        }

        private static void WarnOnce(ref bool warned, string message)
        {
            if (warned)
                return;

            warned = true;
            Log.LogWarning(message);
        }
    }
}
