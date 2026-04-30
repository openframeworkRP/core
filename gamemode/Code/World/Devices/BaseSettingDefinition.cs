using OpenFramework.World.Devices;
using System.Text.Json.Serialization;

namespace OpenFramework.World.Devices;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( BaseDeviceSettings ), "device" )]
[JsonDerivedType( typeof( BaseAppSettings ), "app" )]
public class BaseSettingsDefinition
{
	[Property] public string Id { get; set; }
	[Property] public string Label { get; set; }
	[Property] public string Category { get; set; }
	[Property] public string Value { get; set; }
	[Property] public string Icon { get; set; }
	[Property] public string IconColor { get; set; } = "#8E8E93";

	public Action<object> OnChanged { get; set; }
}

public class BaseDeviceSettings : BaseSettingsDefinition
{
	[Property] public DeviceKind DeviceType { get; set; }
}

public class BaseAppSettings : BaseSettingsDefinition { }
