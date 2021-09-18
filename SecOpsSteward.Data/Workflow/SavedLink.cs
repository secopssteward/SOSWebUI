namespace SecOpsSteward.Data.Workflow
{
    public class SavedLink
    {
        public string Id { get; set; }
        public string SourceNodeId { get; set; }
        public string SourceOutputCode { get; set; }
        public string TargetNodeId { get; set; }
    }
}