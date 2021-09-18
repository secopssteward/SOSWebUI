using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SecOpsSteward.Data.Workflow;
using SecOpsSteward.Shared.Messages;

namespace SecOpsSteward.Data.Models
{
    public class WorkflowModel
    {
        [Key] public Guid WorkflowId { get; set; } = Guid.NewGuid();

        [NotMapped]
        public SavedWorkflow SavedData
        {
            get
            {
                if (string.IsNullOrEmpty(WorkflowJson)) return null;
                return JsonSerializer.Deserialize<SavedWorkflow>(WorkflowJson);
            }
            set => WorkflowJson = JsonSerializer.Serialize(value);
        }

        public string WorkflowJson { get; set; }

        [NotMapped]
        public WorkflowExecutionMessage WorkflowAuthorization
        {
            get
            {
                if (string.IsNullOrEmpty(WorkflowAuthorizationJson)) return null;
                return JsonSerializer.Deserialize<WorkflowExecutionMessage>(WorkflowAuthorizationJson);
            }
            set => WorkflowAuthorizationJson = JsonSerializer.Serialize(value);
        }

        public string WorkflowAuthorizationJson { get; set; }

        [NotMapped]
        public bool IsLocked =>
            WorkflowAuthorization != null &&
            WorkflowAuthorization.Signature != null &&
            WorkflowAuthorization.Signature.IsSigned;

        public bool IsAgentSetGranted { get; set; }

        public Guid? GrantingUserId { get; set; }
        public UserModel GrantingUser { get; set; }

        public string Name { get; set; } = "New Workflow";
        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
    }
}