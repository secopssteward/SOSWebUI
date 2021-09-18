using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SecOpsSteward.Data.Models
{
    public class AgentModel
    {
        [Key] public Guid AgentId { get; set; } = Guid.NewGuid();

        public string Tag { get; set; } = "Agent";

        public string Identity { get; set; }

        public bool Enabled { get; set; } = true;

        public ICollection<AgentPermissionModel> Permissions { get; set; }
        public ICollection<AgentGrantModel> AgentPackageGrants { get; set; }
    }
}