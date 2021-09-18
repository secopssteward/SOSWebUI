using Blazor.Diagrams.Core.Models;

namespace SecOpsSteward.UI.Pages.Workflows.Composer.Links
{
    public class InputPort : CommonPortModel
    {
        public InputPort(NodeModel parent, PortAlignment alignment) : base(parent, alignment)
        {
        }

        public override bool CanAttachTo(PortModel port)
        {
            if (!base.CanAttachTo(port))
                return false;

            return port is OutputPort;
        }
    }
}