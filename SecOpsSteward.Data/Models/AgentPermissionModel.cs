using System;
using System.ComponentModel.DataAnnotations;

namespace SecOpsSteward.Data.Models
{
    public class AgentPermissionModel
    {
        [Key] public Guid AgentPermissionId { get; set; } = Guid.NewGuid();

        public Guid AgentId { get; set; }
        public Guid UserId { get; set; }
        public Guid PackageId { get; set; }

        public AgentModel Agent { get; set; }

        public UserModel User { get; set; }

        public PluginMetadataModel Package { get; set; }
    }
}