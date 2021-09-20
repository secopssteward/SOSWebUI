using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using SecOpsSteward.Data.Workflow;
using SecOpsSteward.Shared.Messages;

namespace SecOpsSteward.Data.Models
{
    public class WorkflowExecutionModel
    {
        [Key] public Guid ExecutionId { get; set; }

        public Guid WorkflowId { get; set; }
        public WorkflowModel Workflow { get; set; }

        public Guid RecurrenceId { get; set; }
        public WorkflowRecurrenceModel Recurrence { get; set; }

        public bool BasedOnLockedWorkflow { get; set; }

        public DateTimeOffset RunStarted { get; set; }

        [NotMapped]
        public SavedWorkflow SavedWorkflow
        {
            get => JsonSerializer.Deserialize<SavedWorkflow>(SavedWorkflowJson);
            set => SavedWorkflowJson = JsonSerializer.Serialize(value);
        }

        public string SavedWorkflowJson { get; set; }

        [NotMapped]
        public WorkflowReceipt WorkflowReceipt
        {
            get => JsonSerializer.Deserialize<WorkflowReceipt>(WorkflowReceiptJson);
            set => WorkflowReceiptJson = JsonSerializer.Serialize(value);
        }

        public string WorkflowReceiptJson { get; set; }

        [NotMapped]
        public List<Guid> Approvers
        {
            get => ApproversString.Split(';').Select(s => Guid.Parse(s)).ToList();
            set => ApproversString = string.Join(';', value);
        }

        public string ApproversString { get; set; }
    }
}