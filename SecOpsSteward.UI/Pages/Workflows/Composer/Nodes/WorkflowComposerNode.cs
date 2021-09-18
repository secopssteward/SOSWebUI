using System;
using System.Collections.Generic;
using System.Linq;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using SecOpsSteward.Data.Models;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.Messages;
using SecOpsSteward.UI.Pages.Workflows.Composer.Links;

namespace SecOpsSteward.UI.Pages.Workflows.Composer.Nodes
{
    public class WorkflowComposerNode : NodeModel
    {
        public WorkflowComposerNode(string id, PluginMetadataModel package, double x, double y) : base(id,
            new Point(x, y))
        {
            ApplyPackage(package);
        }

        public WorkflowComposerNode(PluginMetadataModel package, double x, double y) : base(new Point(x, y))
        {
            ApplyPackage(package);
        }

        public PluginMetadataModel Package { get; set; }
        public ConfigurableObjectParameterCollection Parameters { get; set; }
        public ExecutionStepReceipt Receipt { get; set; }
        public Guid AgentId { get; set; }
        public Guid WorkflowStepId { get; set; } = Guid.NewGuid();
        public string NodeName { get; set; } = "Workflow Node";

        public bool IsSelected { get; set; }
        public NodeStates Success { get; set; } = NodeStates.None;

        public InputPort SourcePort => Ports.OfType<InputPort>().FirstOrDefault();

        public Dictionary<string, OutputPort> ResultPorts =>
            Ports.OfType<OutputPort>().ToDictionary(k => k.OutputCode, v => v);

        private void ApplyPackage(PluginMetadataModel package)
        {
            if (string.IsNullOrEmpty(NodeName)) NodeName = "Node - " + WorkflowStepId.ShortId();
            Package = package;
            Parameters = package.Contract.Clone();
            AddPort(new InputPort(this, PortAlignment.Top));
            if (Package.PossibleOutputs.Count > 3)
                throw new Exception("Supports a maximum of 3 outputs!");
            var positions = new[]
            {
                PortAlignment.BottomLeft,
                PortAlignment.BottomRight,
                PortAlignment.Bottom
            };
            var pos = 0;
            foreach (var output in Package.PossibleOutputs)
                AddPort(new OutputPort(this, output, positions[pos++ % 3]));
        }
    }

    public enum NodeStates
    {
        None,
        Failed,
        Success,
        Skipped
    }
}