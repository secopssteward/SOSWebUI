using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Plugins.WorkflowTemplates;

namespace SecOpsSteward.Data.Models
{
    public class WorkflowTemplateModel
    {
        [Key] public Guid WorkflowTemplateId { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        public Guid ManagedServiceId { get; set; }
        public ManagedServiceModel ManagedService { get; set; }

        [NotMapped]
        public ConfigurableObjectParameterCollection Configuration
        {
            get => JsonSerializer.Deserialize<ConfigurableObjectParameterCollection>(ConfigurationJson);
            set => ConfigurationJson = JsonSerializer.Serialize(value);
        }

        public string ConfigurationJson { get; set; }

        public ICollection<WorkflowTemplateParticipantModel> Participants { get; set; }

        /// <summary>
        ///     Returns a list of IDs in indexable order (for applying config)
        /// </summary>
        /// <returns>Ordered plugin IDs</returns>
        public List<Guid> GetPluginIdsInOrder()
        {
            return Participants.OrderBy(p => p.Index).Select(p => p.PackageId).ToList();
        }

        /// <summary>
        ///     Map values input by a user to the Configuration to their Plugins (by participant index).
        ///     Plugin list is expected to be in order (to apply index).
        /// </summary>
        /// <param name="templateConfiguration"></param>
        /// <param name="plugins"></param>
        public void MapTemplateConfigurationToPlugins(
            Dictionary<string, object> templateConfiguration,
            List<ConfigurableObjectParameterCollection> pluginConfigurations)
        {
            var orderedParticipants = Participants.OrderBy(p => p.Index);
            var indexAdjustment = 0;
            foreach (var participant in orderedParticipants)
            {
                if (participant.PackageId == Guid.Empty)
                {
                    indexAdjustment -= 1;
                    continue;
                }

                // todo: use pluginId+idx instead of just idx?
                // map config and apply it by plugin index (matching Id as a check)
                var thisMappedConfig = participant.ConfigurationMappings.ToDictionary(k => k.Value,
                    v => templateConfiguration.GetValueOrDefault(v.Key));
                var correspondingPluginConfig = pluginConfigurations[participant.Index + indexAdjustment];

                foreach (var mapping in thisMappedConfig)
                {
                    if (!correspondingPluginConfig.Parameters.Any(p => p.Name == mapping.Key)) continue;
                    correspondingPluginConfig[mapping.Key] = mapping.Value;
                }
            }
        }

        public static WorkflowTemplateModel FromMetadata(WorkflowTemplateDefinition template)
        {
            return new()
            {
                WorkflowTemplateId = template.WorkflowTemplateId,
                Configuration = template.Configuration,
                Name = template.Name,
                Participants = template.Participants.Select(p =>
                    WorkflowTemplateParticipantModel.FromMetadata(p, template.Participants.IndexOf(p))).ToList()
            };
        }
    }
}