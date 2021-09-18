using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace SecOpsSteward.UI.Pages.Workflows.Composer.Links
{
    public abstract class CommonPortModel : PortModel
    {
        protected CommonPortModel(NodeModel parent, PortAlignment alignment = PortAlignment.Bottom,
            Point position = null, Size size = null) : base(parent, alignment, position, size)
        {
        }

        public bool IsActivated { get; set; }
        public bool IsSkipped { get; set; }
    }
}