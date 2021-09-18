using System;
using System.ComponentModel.DataAnnotations;

namespace SecOpsSteward.Data.Models
{
    public class AgentGrantModel
    {
        [Key] public Guid GrantId { get; set; } = Guid.NewGuid();

        public Guid AgentId { get; set; }
        public AgentModel Agent { get; set; }

        public Guid PluginId { get; set; }
        public PluginMetadataModel Plugin { get; set; }

        public int AuthorizationScopeHashcode { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public Guid UserPerformingGrantId { get; set; }
        public UserModel UserPerformingGrant { get; set; }
    }
}