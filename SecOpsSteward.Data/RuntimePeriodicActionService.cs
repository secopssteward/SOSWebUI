using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SecOpsSteward.Data.Models;
using SecOpsSteward.Shared.Cryptography.Extensions;
using SecOpsSteward.Shared.Messages;

namespace SecOpsSteward.Data
{
    public class RuntimePeriodicActionService
    {
        private readonly ICryptographicService _cryptoService;
        private readonly SecOpsStewardDbContext _dbContext;
        private readonly IMessageTransitService _messageTransit;

        public RuntimePeriodicActionService(
            SecOpsStewardDbContext dbContext,
            IMessageTransitService messageTransit,
            ICryptographicService cryptoService)
        {
            _dbContext = dbContext;
            _messageTransit = messageTransit;
            _cryptoService = cryptoService;
        }

        public async Task PerformPeriodicActions()
        {
            await Task.WhenAll(_dbContext.WorkflowRecurrences
                .ToList()
                .Where(wfr => wfr.Approvers.Count >= wfr.NumberOfApproversRequired)
                .Where(wfr => wfr.ShouldBeRun)
                .Select(wfr => ProcessRecurrence(wfr)));
        }

        public async Task ProcessRecurrence(WorkflowRecurrenceModel recurrence)
        {
            _dbContext.WorkflowExecutions.Add(new WorkflowExecutionModel
            {
                Approvers = recurrence.Approvers,
                Recurrence = recurrence,
                RunStarted = DateTimeOffset.UtcNow,
                Workflow = recurrence.Workflow
            });
            recurrence.MostRecentRun = DateTimeOffset.UtcNow;
            recurrence.Approvers = new List<Guid>();
            await _dbContext.SaveChangesAsync();

            var msg = recurrence.Workflow.WorkflowAuthorization;
            foreach (var step in msg.GetNextSteps())
            {
                var encrypted = await msg.Encrypt(_cryptoService, step.RunningEntity);
                var envelope = new EncryptedMessageEnvelope(encrypted);
                await _messageTransit.Enqueue(envelope);
            }
        }
    }
}