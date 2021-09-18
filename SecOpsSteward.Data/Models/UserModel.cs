using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SecOpsSteward.Shared;

namespace SecOpsSteward.Data.Models
{
    public class UserModel
    {
        [Key] public Guid UserId { get; set; } = Guid.NewGuid();

        public string Username { get; set; }

        public string DisplayName { get; set; }

        public ChimeraUserRole Role { get; set; } = ChimeraUserRole.None;

        public ICollection<AgentPermissionModel> Permissions { get; set; }
        public ICollection<AgentGrantModel> AgentPackageGrants { get; set; }
    }
}