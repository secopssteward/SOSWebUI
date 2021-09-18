using System.Collections.Generic;
using System.Linq;
using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using SecOpsSteward.Data.Models;
using SecOpsSteward.Data.Workflow;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.Messages;
using SecOpsSteward.UI.Pages.Workflows.Composer.Links;
using SecOpsSteward.UI.Pages.Workflows.Composer.Nodes;

namespace SecOpsSteward.UI.Pages.Workflows.Composer
{
    public static class SavedWorkflowExtensions
    {
        public static SavedWorkflow SaveWorkflow(this Diagram diagram)
        {
            var savedWf = new SavedWorkflow();
            savedWf.Nodes = diagram.Nodes.Cast<WorkflowComposerNode>().Select(model =>
                new SavedNode
                {
                    Id = model.Id,
                    X = model.Position.X,
                    Y = model.Position.Y,
                    W = model.Size.Width,
                    H = model.Size.Height,
                    AgentId = model.AgentId,
                    PackageId = model.Package.PluginId,
                    WorkflowStepId = model.WorkflowStepId,
                    NodeName = model.NodeName,
                    Parameters = model.Parameters
                }).ToList();
            savedWf.Links = diagram.Links.Select(link =>
                new SavedLink
                {
                    Id = link.Id,
                    SourceNodeId = link.SourceNode.Id,
                    TargetNodeId = link.TargetPort.Parent.Id,
                    SourceOutputCode = (link.SourcePort as OutputPort).OutputCode
                }).ToList();

            return savedWf;
        }

        public static void LoadWorkflow(this Diagram diagram, SavedWorkflow workflow,
            IEnumerable<PluginMetadataModel> packages)
        {
            foreach (var node in workflow.Nodes)
                diagram.Nodes.Add(AsWorkflowComposerNode(node, packages));
            foreach (var link in workflow.Links)
            {
                var linkModels = new List<LinkModel>();
                var source = diagram.Nodes.First(n => n.Id == link.SourceNodeId);
                var sourcePort = source.Ports.OfType<OutputPort>().First(p => p.OutputCode == link.SourceOutputCode);
                var target = diagram.Nodes.First(n => n.Id == link.TargetNodeId);
                var targetPort = target.Ports.OfType<InputPort>().First();

                diagram.Links.Add(new LinkModel(link.Id, sourcePort, targetPort));
            }
        }

        public static WorkflowComposerNode AsWorkflowComposerNode(this SavedNode node,
            IEnumerable<PluginMetadataModel> packages)
        {
            var wfNode = new WorkflowComposerNode(
                node.Id,
                packages.First(p => p.PluginId == node.PackageId),
                node.X, node.Y);
            wfNode.NodeName = node.NodeName;
            wfNode.AgentId = node.AgentId;
            wfNode.Size = new Size(node.W, node.H);
            wfNode.Parameters = node.Parameters.Clone();
            wfNode.WorkflowStepId = node.WorkflowStepId;
            return wfNode;
        }

        public static ExecutionStepCollection CreateStepCollectionFromWorkflow(this WorkflowModel workflowModel)
        {
            return CreateStepCollectionFromWorkflow(
                ChimeraSharedHelpers.GetFromSerializedString<SavedWorkflow>(workflowModel.WorkflowJson));
        }

        public static ExecutionStepCollection CreateStepCollectionFromWorkflow(this SavedWorkflow savedWorkflow)
        {
            //var savedWorkflow = ChimeraSharedHelpers.GetFromSerializedString<SavedWorkflow>(model.WorkflowJson);
            var nodesWithLinks = savedWorkflow.Nodes.Select(n => new SavedNodeWithLink(n)).ToList();
            foreach (var n in nodesWithLinks)
            {
                var linksOut = savedWorkflow.Links.Where(l => l.SourceNodeId == n.Id);
                foreach (var l in linksOut)
                {
                    if (!n.LinksOut.ContainsKey(l.SourceOutputCode))
                        n.LinksOut[l.SourceOutputCode] = new List<SavedNodeWithLink>();
                    var target = savedWorkflow.Nodes.First(n => n.Id == l.TargetNodeId);
                    n.LinksOut[l.SourceOutputCode].Add(nodesWithLinks.First(n => n.Id == target.Id));
                }
            }

            var allLinkTargets =
                nodesWithLinks.SelectMany(l => l.LinksOut.SelectMany(lo => lo.Value.Select(v => v.Id)));
            var noLinksIn = nodesWithLinks.Where(l => !allLinkTargets.Contains(l.Id));
            var rootStep = noLinksIn.FirstOrDefault();

            var steps = new ExecutionStepCollection();
            var parent = steps.AddStepWithoutSigning(rootStep.AgentId, rootStep.PackageId, null,
                ChimeraSharedHelpers.SerializeToString(rootStep.Parameters.AsDictionary()));
            rootStep.AuthorizingMessage = parent;
            foreach (var next in rootStep.LinksOut)
                AddStepsToCollection(steps, parent, next);
            return steps;
        }

        private static void AddStepsToCollection(ExecutionStepCollection steps, ExecutionStep parentStep,
            KeyValuePair<string, List<SavedNodeWithLink>> lastOutputs)
        {
            foreach (var thisStep in lastOutputs.Value)
            {
                var parent = steps.AddStepWithoutSigning(parentStep, lastOutputs.Key, thisStep.AgentId,
                    thisStep.PackageId, null,
                    ChimeraSharedHelpers.SerializeToString(thisStep.Parameters.AsDictionary()));
                thisStep.AuthorizingMessage = parent;
                foreach (var next in thisStep.LinksOut)
                    AddStepsToCollection(steps, parent, next);
            }
        }

        private class SavedNodeWithLink : SavedNode
        {
            public SavedNodeWithLink(SavedNode n)
            {
                Id = n.Id;
                AgentId = n.AgentId;
                PackageId = n.PackageId;
                Parameters = n.Parameters;
                WorkflowStepId = n.WorkflowStepId;
            }

            public Dictionary<string, List<SavedNodeWithLink>> LinksOut { get; } =
                new();

            public ExecutionStep AuthorizingMessage { get; set; }
        }
    }
}