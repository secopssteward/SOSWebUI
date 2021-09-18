using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.Packaging.Metadata;

namespace SecOpsSteward.Data.Models
{
    public class ManagedServiceModel
    {
        [Key] public Guid ManagedServiceId { get; set; }

        public string Name { get; set; }

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

        public ContainerModel Container { get; set; }
        public Guid ContainerId { get; set; }

        public ICollection<PluginMetadataModel> Plugins { get; set; } = new List<PluginMetadataModel>();
        public ICollection<WorkflowTemplateModel> Templates { get; set; } = new List<WorkflowTemplateModel>();

        public static ManagedServiceModel FromMetadata(ServiceMetadata svc, PluginMetadata[] plugins)
        {
            return new()
            {
                ManagedServiceId =
                    svc.ServiceId.GetComponents(PackageIdentifierComponents.Container |
                                                PackageIdentifierComponents.Service),
                Name = svc.Name,
                Description = svc.Description,
                Contract = svc.ParameterCollection.Clone(),
                Templates = svc.Templates.Select(template => WorkflowTemplateModel.FromMetadata(template)).ToList(),
                Plugins = plugins.Select(plugin => PluginMetadataModel.FromMetadata(plugin)).ToList()
            };
        }
    }
}