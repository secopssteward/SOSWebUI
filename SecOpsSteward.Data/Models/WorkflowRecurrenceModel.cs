using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SecOpsSteward.Data.Models
{
    public class WorkflowRecurrenceModel
    {
        [Key] public Guid WorkflowRecurrenceId { get; set; }

        public Guid WorkflowId { get; set; }

        public DateTimeOffset FirstRun { get; set; }
        public DateTimeOffset MostRecentRun { get; set; }
        public TimeSpan TimeBetweenRuns { get; set; }

        public int NumberOfApproversRequired { get; set; }

        [NotMapped]
        public List<Guid> Approvers
        {
            get => ApproversString.Split(';').Select(s => Guid.Parse(s)).ToList();
            set => ApproversString = string.Join(';', value);
        }

        public string ApproversString { get; set; }

        [NotMapped] public bool ShouldBeRun => DateTimeOffset.UtcNow < MostRecentRun.Add(TimeBetweenRuns);

        public WorkflowModel Workflow { get; set; }

        public ICollection<WorkflowExecutionModel> Executions { get; set; }
    }
}