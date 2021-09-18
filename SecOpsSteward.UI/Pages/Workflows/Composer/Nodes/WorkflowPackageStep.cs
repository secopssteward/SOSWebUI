using System.Collections.Generic;
using System.Linq;
using Blazor.Diagrams.Core;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.Messages;
using SecOpsSteward.UI.Pages.Workflows.Composer.Links;

namespace SecOpsSteward.UI.Pages.Workflows.Composer.Nodes
{
    public class WorkflowPackageStep
    {
        public WorkflowPackageStep(WorkflowComposerNode node, string outputComingIn) : this(node)
        {
            RequiredInputCode = outputComingIn;
        }

        public WorkflowPackageStep(WorkflowComposerNode node)
        {
            PackageNode = node;
            PackageId = node.Package.PluginId;
            Configuration = node.Parameters.AsSerializedString();
            AgentId = node.AgentId;

            // over each output
            foreach (var output in node.Ports.OfType<OutputPort>())
                // over the N links in that output
            foreach (var link in output.Links)
            {
                var targetPort = link.TargetPort;
                if (targetPort == null) continue;
                var targetNode = targetPort.Parent as WorkflowComposerNode;
                if (targetNode == null) continue;
                NextSteps.Add(new WorkflowPackageStep(targetNode, output.OutputCode));
            }
        }

        public ChimeraPackageIdentifier PackageId { get; set; }

        public ChimeraAgentIdentifier AgentId { get; set; }

        public ExecutionStep AuthorizingMessage { get; set; }

        public string RequiredInputCode { get; set; }

        public string Configuration { get; set; }

        public List<WorkflowPackageStep> NextSteps { get; set; } = new();

        public WorkflowComposerNode PackageNode { get; set; }

        public static WorkflowPackageStep GetExecutionTreeFromDiagram(Diagram diagram)
        {
            return new WorkflowPackageStep(diagram.Nodes
                .Where(n => !n.Links.Any(l => l.TargetPort is InputPort))
                .Cast<WorkflowComposerNode>()
                .FirstOrDefault());
        }

        public List<WorkflowPackageStep> GetCollapsedSteps()
        {
            var nodes = new List<WorkflowPackageStep>();
            GetCollapsedSteps(nodes);
            return nodes;
        }

        private void GetCollapsedSteps(List<WorkflowPackageStep> nodes)
        {
            nodes.Add(this);
            foreach (var item in NextSteps)
                item.GetCollapsedSteps(nodes);
        }
    }
}