using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker.Database
{
    public sealed class SnowflakeConverter : ValueConverter<Snowflake, ulong>
    {
        public SnowflakeConverter() : base(snowflake => snowflake.Value, value => DiscordSnowflake.New(value)) { }
    }
}
