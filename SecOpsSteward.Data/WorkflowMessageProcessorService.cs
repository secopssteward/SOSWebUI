using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.EntityFrameworkCore;
using SecOpsSteward.Shared.Cryptography.Extensions;
using SecOpsSteward.Shared.DiscoveryWorkflow;
using SecOpsSteward.Shared.Messages;
using SecOpsSteward.Shared.Roles;

namespace SecOpsSteward.Data
{
    public class WorkflowMessageProcessorService : IDisposable
    {
        private readonly ICryptographicService _cryptoService;
        private readonly TokenOwner _currentUser;
        private readonly IDbContextFactory<SecOpsStewardDbContext> _dbContextFactory;
        private readonly IMessageTransitService _messageTransit;
        private readonly WorkflowProcessorFactory _wfFactory;

        private bool _started;
        private Timer _timer = new();
        private bool disposedValue;
        private Func<EncryptedMessageEnvelope, Task<bool>> ExecutionApproval;
        private readonly Dictionary<Guid, Func<ExecutionStepReceipt, Task>> StepReceiptCallbacks = new();

        private readonly Dictionary<Guid, Func<WorkflowReceipt, Task>> WorkflowReceiptCallbacks = new();

        public WorkflowMessageProcessorService(
            IDbContextFactory<SecOpsStewardDbContext> dbContextFactory,
            ICryptographicService cryptoService,
            IMessageTransitService messageTransit,
            WorkflowProcessorFactory wfFactory,
            TokenOwner currentUser)
        {
            _dbContextFactory = dbContextFactory;
            _cryptoService = cryptoService;
            _messageTransit = messageTransit;
            _wfFactory = wfFactory;
            _currentUser = currentUser;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void SetExecutionApproval(Func<EncryptedMessageEnvelope, Task<bool>> cb)
        {
            ExecutionApproval = cb;
        }

        public Guid SubscribeStepReceipt(Func<ExecutionStepReceipt, Task> cb)
        {
            var newGuid = Guid.NewGuid();
            StepReceiptCallbacks.Add(newGuid, cb);
            return newGuid;
        }

        public Guid SubscribeWorkflowReceipt(Func<WorkflowReceipt, Task> cb)
        {
            var newGuid = Guid.NewGuid();
            WorkflowReceiptCallbacks.Add(newGuid, cb);
            return newGuid;
        }

        public void Unsubscribe(Guid id)
        {
            if (WorkflowReceiptCallbacks.ContainsKey(id))
                WorkflowReceiptCallbacks.Remove(id);
            if (StepReceiptCallbacks.ContainsKey(id))
                StepReceiptCallbacks.Remove(id);
        }

        public void StartBackgroundTask(int milliseconds = 15000)
        {
            if (_started) return;
            _started = true;

            _timer = new Timer();
            _timer.Interval = milliseconds;
            _timer.AutoReset = true;
            _timer.Elapsed += async (o, t) => { await ProcessUserQueue(); };
            _timer.Start();

            Task.Run(() => ProcessUserQueue());
        }

        public void StopBackgroundTask()
        {
            _timer.Stop();
            _timer = null;
            _started = false;
        }

        public async Task ProcessUserQueue()
        {
            try
            {
                await _messageTransit.DequeuePoll(_currentUser.UserId, async (m, c) =>
                {
                    if (m.Payload.MessageType == typeof(WorkflowReceipt).Name)
                    {
                        var workflowReceipt = await m.Payload.Decrypt<WorkflowReceipt>(_cryptoService);

                        using (var cxt = _dbContextFactory.CreateDbContext())
                        {
                            if (cxt.WorkflowExecutions.Any(e => e.WorkflowId == workflowReceipt.WorkflowId))
                            {
                                // TODO: with SQLite, we can't directly order by time -- remove ToList later
                                var execution = cxt.WorkflowExecutions.ToList().OrderByDescending(e => e.RunStarted)
                                    .First();
                                execution.WorkflowReceipt = workflowReceipt;
                                await cxt.SaveChangesAsync();
                            }
                        }

                        await Fire(workflowReceipt);
                    }
                    else if (m.Payload.MessageType == typeof(ExecutionStepReceipt).Name)
                    {
                        var stepReceipt = await m.Payload.Decrypt<ExecutionStepReceipt>(_cryptoService);

                        await Fire(stepReceipt);
                    }
                    else if (m.Payload.MessageType == typeof(WorkflowExecutionMessage).Name)
                    {
                        if (ExecutionApproval == null || !await ExecutionApproval(m)) return MessageActions.Defer;
                        var processor = _wfFactory.GetWorkflowProcessor(_currentUser.UserId, _currentUser.Name, m);
                        _ = Task.Run(() => processor.Run());
                    }

                    return MessageActions.Complete;
                }, TimeSpan.FromSeconds(10));
            }
            catch
            {
            }
        }

        public async Task<Guid> EnqueueImmediateRun(ExecutionStepCollection steps)
        {
            var msg = new WorkflowExecutionMessage(_currentUser.UserId, steps);
            msg.Conditions.ValidFrom = DateTimeOffset.UtcNow;
            msg.Conditions.ValidTo = DateTimeOffset.UtcNow.AddMinutes(15);
            msg.Conditions.MaximumNumberOfRuns = 1;

            await Task.WhenAll(msg.Steps.Select(s =>
                s.Sign(_cryptoService, _currentUser.UserId, $"{_currentUser.UserId.ShortId}")));
            await msg.Sign(_cryptoService, _currentUser.UserId, $"{_currentUser.UserId.ShortId}");

            await Enqueue(msg);
            return msg.WorkflowId;
        }

        public async Task Enqueue(WorkflowExecutionMessage msg)
        {
            await Task.WhenAll(msg.GetNextSteps().Select(async s =>
            {
                var encrypted = await msg.Encrypt(_cryptoService, s.RunningEntity);
                var envelope = new EncryptedMessageEnvelope(encrypted);
                await _messageTransit.Enqueue(envelope);
            }));
        }

        private Task Fire(WorkflowReceipt receipt)
        {
            return Task.WhenAll(WorkflowReceiptCallbacks.Select(cb =>
            {
                try
                {
                    return WorkflowReceiptCallbacks[cb.Key](receipt);
                }
                catch
                {
                    Unsubscribe(cb.Key);
                }

                return Task.CompletedTask;
            }));
        }

        private Task Fire(ExecutionStepReceipt receipt)
        {
            return Task.WhenAll(WorkflowReceiptCallbacks.Select(cb =>
            {
                try
                {
                    return StepReceiptCallbacks[cb.Key](receipt);
                }
                catch
                {
                    Unsubscribe(cb.Key);
                }

                return Task.CompletedTask;
            }));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StepReceiptCallbacks.Clear();
                    WorkflowReceiptCallbacks.Clear();
                    _timer.Stop();
                    _timer = null;
                }

                disposedValue = true;
            }
        }
    }
}