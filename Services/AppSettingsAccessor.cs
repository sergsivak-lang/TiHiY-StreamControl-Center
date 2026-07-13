using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class AppSettingsAccessor
{
    public AppSettings Value { get; set; } = new();
}
