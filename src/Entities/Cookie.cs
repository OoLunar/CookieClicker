using System;
using System.ComponentModel.DataAnnotations;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record Cookie
    {
        [Key]
        public Ulid Id { get; init; } = Ulid.NewUlid();
        public decimal Clicks;
        public Snowflake GuildId { get; init; }
        public Snowflake ChannelId { get; set; }
        public Snowflake MessageId { get; set; }

        public Cookie() { }

        public Cookie(Snowflake guildId, Snowflake channelId, Snowflake messageId)
        {
            GuildId = guildId;
            ChannelId = channelId;
            MessageId = messageId;
        }
    }
}
