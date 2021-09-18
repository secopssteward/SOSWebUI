using Blazor.Diagrams.Core.Models;

namespace SecOpsSteward.UI.Pages.Workflows.Composer.Links
{
    public class OutputPort : CommonPortModel
    {
        public OutputPort(NodeModel parent, string outputCode, PortAlignment alignment) : base(parent, alignment)
        {
            OutputCode = outputCode;
        }

        public string OutputCode { get; set; }

        public override bool CanAttachTo(PortModel port)
        {
            if (!base.CanAttachTo(port))
                return false;

            return port is InputPort;
        }
    }
}