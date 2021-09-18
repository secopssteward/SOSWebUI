using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SecOpsSteward.Plugins.WorkflowTemplates;

namespace SecOpsSteward.Data.Models
{
    public class WorkflowTemplateParticipantModel
    {
        [Key] public Guid WorkflowTemplateParticipantId { get; set; } = Guid.NewGuid();

        public Guid WorkflowTemplateId { get; set; }
        public WorkflowTemplateModel WorkflowTemplate { get; set; }

        public int Index { get; set; }
        public Guid PackageId { get; set; }

        [NotMapped]
        public Dictionary<string, string> ConfigurationMappings
        {
            get => JsonSerializer.Deserialize<Dictionary<string, string>>(MappingJson);
            set => MappingJson = JsonSerializer.Serialize(value);
        }

        public string MappingJson { get; set; }

        public static WorkflowTemplateParticipantModel FromMetadata(WorkflowTemplateParticipantDefinition p, int idx)
        {
            return new()
            {
                PackageId = p.PackageId,
                Index = idx,
                ConfigurationMappings = p.ConfigurationMappings
            };
        }
    }
}