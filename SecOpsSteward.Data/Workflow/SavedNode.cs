using System;
using System.Linq;
using SecOpsSteward.Data.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Data.Workflow
{
    public class SavedNode
    {
        public Guid WorkflowStepId { get; set; }

        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        public Guid PackageId { get; set; }
        public ConfigurableObjectParameterCollection Parameters { get; set; }
        public string NodeName { get; set; }
        public Guid AgentId { get; set; }

        public PluginMetadataModel Package { get; set; }
        public AgentModel Agent { get; set; }
        public ManagedServiceModel Service => Package?.ManagedService;
        public AgentGrantModel Grant { get; set; }
        public int AuthorizationHashCode => Parameters.GetConfigurationGrantScopeHashCode();

        public void PopulateDatabaseLinks(SecOpsStewardDbContext dbContext)
        {
            // TODO: Nav properties not mapping this??? Why?
            Package = dbContext.Plugins.FirstOrDefault(p => p.PluginId == PackageId);
            var svc = dbContext.ManagedServices.FirstOrDefault(s => s.Plugins.Contains(Package));
            Package.ManagedService = svc;
            Agent = dbContext.Agents.FirstOrDefault(a => a.AgentId == AgentId);
            Grant = dbContext.AgentGrants.FirstOrDefault(g => g.AgentId == AgentId &&
                                                              g.PluginId == PackageId &&
                                                              g.AuthorizationScopeHashcode ==
                                                              Parameters.GetConfigurationGrantScopeHashCode());
        }
    }
}