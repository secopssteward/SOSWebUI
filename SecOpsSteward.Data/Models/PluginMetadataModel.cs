using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Shared.Packaging.Metadata;

namespace SecOpsSteward.Data.Models
{
    public class PluginMetadataModel
    {
        [Key] public Guid PluginId { get; set; } = Guid.NewGuid();

        public Guid ManagedServiceId { get; set; }

        public ManagedServiceModel ManagedService { get; set; }

        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }

        [NotMapped]
        public ConfigurableObjectParameterCollection Contract
        {
            get
            {
                if (string.IsNullOrEmpty(ContractJson)) return null;
                return JsonSerializer.Deserialize<ConfigurableObjectParameterCollection>(ContractJson);
            }
            set => ContractJson = JsonSerializer.Serialize(value);
        }

        public string ContractJson { get; set; }

        [NotMapped]
        public List<string> PossibleOutputs
        {
            get => PossibleOutputsString.Split(";").ToList();
            set => PossibleOutputsString = string.Join(";", value);
        }

        public string PossibleOutputsString { get; set; } = string.Empty;

        [NotMapped]
        public List<string> TransitionInputs
        {
            get => TransitionInputsString.Split(";").ToList();
            set => TransitionInputsString = string.Join(";", value);
        }

        public string TransitionInputsString { get; set; } = string.Empty;

        [NotMapped]
        public List<string> TransitionOutputs
        {
            get => TransitionOutputsString.Split(";").ToList();
            set => TransitionOutputsString = string.Join(";", value);
        }

        public string TransitionOutputsString { get; set; } = string.Empty;

        public ICollection<AgentPermissionModel> Permissions { get; set; }
        public ICollection<AgentGrantModel> AgentPackageGrants { get; set; }

        public static PluginMetadataModel FromMetadata(PluginMetadata plugin)
        {
            return new()
            {
                PluginId = plugin.PluginId.Id,
                Name = plugin.Name,
                Author = plugin.Author,
                Description = plugin.Description,
                Contract = plugin.ParameterCollection.Clone(),
                PossibleOutputs = plugin.Outputs.ToList(),
                TransitionInputs = plugin.TransitionInputs != null
                    ? plugin.TransitionInputs.ToList()
                    : new List<string>(),
                TransitionOutputs = plugin.TransitionOutputs != null
                    ? plugin.TransitionOutputs.ToList()
                    : new List<string>()
            };
        }
    }
}